using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace KupoTradeMaster.Services;

public sealed class WatchlistPoller : IDisposable
{
    private readonly UniversalisClient _client;
    private readonly Configuration _config;
    private readonly IPluginLog _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<uint, DateTime> _lastFetchedUtc = new();

    public ConcurrentDictionary<uint, UniversalisItem> LatestSnapshot { get; } = new();
    public double SessionMaxRawScore { get; private set; } = 1.0;

    public event Action? SnapshotChanged;

    private Task? _loop;
    private volatile bool _forceRefresh;

    private Plugin? _plugin; // Late-bound to break the Plugin/Poller ctor cycle; FlipScore uses home world when set.

    public WatchlistPoller(UniversalisClient client, Configuration config, IPluginLog log)
    {
        _client = client;
        _config = config;
        _log = log;
    }

    /// Set after Plugin construction so ComputeRawScore can source the home world for realistic pricing.
    /// Optional — if null, falls back to DC-wide min (legacy behavior).
    public void AttachPlugin(Plugin plugin) => _plugin = plugin;

    public void Start() => _loop = Task.Run(RunAsync);

    public void ForceRefreshAll()
    {
        _lastFetchedUtc.Clear();
        _forceRefresh = true;
    }

    private async Task RunAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await TickAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.Warning(ex, "KupoTradeMaster poll tick failed");
            }

            var delay = _forceRefresh ? TimeSpan.FromMilliseconds(200) : TimeSpan.FromSeconds(30);
            _forceRefresh = false;
            try { await Task.Delay(delay, _cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var minInterval = TimeSpan.FromSeconds(_config.PerItemMinPollSeconds);

        var due = new List<uint>();
        foreach (var id in _config.Watchlist)
        {
            if (!_lastFetchedUtc.TryGetValue(id, out var last) || (now - last) >= minInterval)
                due.Add(id);
        }

        if (due.Count == 0) return;

        _log.Debug("KupoTradeMaster: fetching {Count} items from Universalis ({Scope})", due.Count, _config.DataScope);
        var results = await _client.FetchAsync(_config.DataScope, due, ct).ConfigureAwait(false);

        foreach (var (id, item) in results)
        {
            LatestSnapshot[id] = item;
            _lastFetchedUtc[id] = now;
        }

        // Recompute session max for score normalization.
        var maxScore = 1.0;
        foreach (var item in LatestSnapshot.Values)
        {
            var s = ComputeRawScore(item);
            if (s > maxScore) maxScore = s;
        }
        SessionMaxRawScore = maxScore;

        SnapshotChanged?.Invoke();
    }

    public double ComputeRawScore(UniversalisItem item)
    {
        if (item.Listings.Count == 0) return 0;

        // Buy at the DC's cheapest world (that's the flip source); sell at your home world's
        // undercut. Using CurrentAveragePrice as the sell reference lets one foreign 2M outlier
        // sale drag the score up for days.
        var home = _plugin?.HomeWorldName;
        var buyMin = item.Listings.Min(l => (double)l.PricePerUnit);
        var sellRef = PriceLens.SellReference(item, home);
        if (sellRef <= 0)
        {
            if (item.CurrentAveragePrice <= 0) return 0;
            sellRef = item.CurrentAveragePrice; // pre-login fallback so scores still exist
        }
        var freshness = (DateTime.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(item.LastUploadTimeMs).UtcDateTime).TotalMinutes;

        var res = FlipScorer.Compute(new FlipScoreInputs(
            MinPriceGil: buyMin,
            SellReferenceGil: sellRef,
            VelocityPerDay: item.RegularSaleVelocity,
            FreshnessMinutes: freshness,
            TaxRate: _config.TaxRate));

        return res.Score;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* swallow */ }
        _cts.Dispose();
    }
}
