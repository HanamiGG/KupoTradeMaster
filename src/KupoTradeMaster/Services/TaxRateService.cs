using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace KupoTradeMaster.Services;

public sealed class TaxRateService : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LoginWaitTimeout = TimeSpan.FromSeconds(60);

    private readonly UniversalisClient _client;
    private readonly Configuration _config;
    private readonly IPlayerState _playerState;
    private readonly IPluginLog _log;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public IReadOnlyDictionary<string, int> LatestByCity { get; private set; } = new Dictionary<string, int>();
    public string? HomeWorldName { get; private set; }
    public string? MinCityName { get; private set; }
    public int? MinRatePercent { get; private set; }
    public DateTime? LastFetchedUtc { get; private set; }

    public event Action? Updated;

    public TaxRateService(UniversalisClient client, Configuration config, IPlayerState playerState, IPluginLog log)
    {
        _client = client;
        _config = config;
        _playerState = playerState;
        _log = log;
    }

    public void Start() => _loop = Task.Run(RunAsync);

    private async Task RunAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var world = await WaitForHomeWorldAsync(_cts.Token).ConfigureAwait(false);
                if (world != null)
                {
                    HomeWorldName = world;
                    var rates = await _client.FetchTaxRatesAsync(world, _cts.Token).ConfigureAwait(false);
                    if (rates != null && rates.Count > 0)
                    {
                        LatestByCity = rates;
                        var minKvp = rates.OrderBy(kv => kv.Value).First();
                        MinCityName = minKvp.Key;
                        MinRatePercent = minKvp.Value;
                        LastFetchedUtc = DateTime.UtcNow;
                        // Guard against bogus 0%/negative/out-of-band values that would corrupt persisted config.
                        if (minKvp.Value is >= 3 and <= 5)
                        {
                            _config.TaxRate = minKvp.Value / 100.0;
                            _config.Save();
                        }
                        Updated?.Invoke();
                        _log.Info("KupoTradeMaster: tax rates fetched for {World}; min={Min}% ({City})", world, minKvp.Value, minKvp.Key);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: tax rate fetch failed"); }

            try { await Task.Delay(RefreshInterval, _cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task<string?> WaitForHomeWorldAsync(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + LoginWaitTimeout;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (_playerState.IsLoaded)
            {
                var world = _playerState.HomeWorld.ValueNullable;
                if (world.HasValue)
                {
                    var name = world.Value.Name.ExtractText();
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        return null;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* swallow */ }
        _cts.Dispose();
    }
}
