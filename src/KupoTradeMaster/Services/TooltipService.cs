using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace KupoTradeMaster.Services;

public sealed class TooltipService : IDisposable
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly IGameGui _gameGui;
    private readonly IFramework _framework;
    private readonly UniversalisClient _client;
    private readonly WatchlistPoller _poller;
    private readonly Configuration _config;
    private readonly IPluginLog _log;

    private readonly ConcurrentDictionary<uint, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<uint, byte> _inflight = new();
    private ulong _lastRawHovered;

    public uint CurrentHoveredItemId { get; private set; }
    public bool CurrentIsHq { get; private set; }
    public UniversalisItem? CurrentData { get; private set; }
    public bool IsFetching { get; private set; }

    public TooltipService(IGameGui gameGui, IFramework framework, UniversalisClient client,
                          WatchlistPoller poller, Configuration config, IPluginLog log)
    {
        _gameGui = gameGui;
        _framework = framework;
        _client = client;
        _poller = poller;
        _config = config;
        _log = log;
        _framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        if (!_config.TooltipEnabled)
        {
            CurrentHoveredItemId = 0;
            _lastRawHovered = 0;
            return;
        }
        var raw = (ulong)_gameGui.HoveredItem;
        if (raw == _lastRawHovered) return;
        _lastRawHovered = raw;
        if (raw == 0 || raw >= 2_000_000)
        {
            CurrentHoveredItemId = 0; CurrentData = null; IsFetching = false; return;
        }
        var isHq = raw >= 1_000_000;
        var id = isHq ? (uint)(raw - 1_000_000) : (uint)raw;
        CurrentHoveredItemId = id;
        CurrentIsHq = isHq;
        if (_poller.LatestSnapshot.TryGetValue(id, out var live))
        {
            CurrentData = live; IsFetching = false; return;
        }
        if (_cache.TryGetValue(id, out var entry) && DateTime.UtcNow - entry.FetchedUtc < CacheTtl)
        {
            CurrentData = entry.Item; IsFetching = false; return;
        }
        CurrentData = null;
        IsFetching = true;
        _ = FetchAsync(id);
    }

    private async Task FetchAsync(uint id)
    {
        if (!_inflight.TryAdd(id, 0)) return;
        try
        {
            var results = await _client.FetchAsync(_config.DataScope, new[] { id }, CancellationToken.None).ConfigureAwait(false);
            var item = results.TryGetValue(id, out var it) ? it : null;
            _cache[id] = new CacheEntry(item, DateTime.UtcNow);
            if (CurrentHoveredItemId == id) { CurrentData = item; IsFetching = false; }
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "KupoTradeMaster: tooltip fetch failed for id={Id}", id);
            if (CurrentHoveredItemId == id) IsFetching = false;
        }
        finally { _inflight.TryRemove(id, out _); }
    }

    public void Dispose() => _framework.Update -= OnFrameworkUpdate;

    private readonly record struct CacheEntry(UniversalisItem? Item, DateTime FetchedUtc);
}
