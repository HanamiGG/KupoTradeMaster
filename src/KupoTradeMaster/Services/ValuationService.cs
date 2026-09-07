using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using KupoTradeMaster.Models;

namespace KupoTradeMaster.Services;

public sealed class ValuationService : IDisposable
{
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PriceCacheTtl    = TimeSpan.FromMinutes(10);

    private static readonly InventoryType[] PlayerContainers =
    {
        InventoryType.Inventory1, InventoryType.Inventory2,
        InventoryType.Inventory3, InventoryType.Inventory4,
        InventoryType.Crystals,
    };

    private static readonly InventoryType[] SaddlebagContainers =
    {
        InventoryType.SaddleBag1, InventoryType.SaddleBag2,
        InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2,
    };

    private readonly UniversalisClient _client;
    private readonly Configuration _config;
    private readonly IGameInventory _inventory;
    private readonly IFramework _framework;
    private readonly IPlayerState _playerState;
    private readonly RetainerService _retainers;
    private readonly IPluginLog _log;
    private readonly string _historyPath;

    private readonly ConcurrentDictionary<uint, PriceCacheEntry> _priceCache = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly List<NetWorthSnapshot> _history = new();
    private readonly object _historySync = new();
    // Serializes ALL disk access to _historyPath — the capture loop's Append, ReloadFromDisk's read,
    // and BackupService's restore-copy. Without this the OS can race File.AppendAllText and
    // File.Create on the same path (Windows raises an IOException, or worse, silently interleaves).
    private readonly object _fileLock = new();

    private Task? _loop;

    public NetWorthSnapshot? Latest { get; private set; }
    public event Action? Changed;

    /// Best-known min-listing price for an item from the valuation cache. 0 if not cached.
    public long GetCachedMinPrice(uint itemId)
        => _priceCache.TryGetValue(itemId, out var pe) ? pe.MinPrice : 0;

    public IReadOnlyList<NetWorthSnapshot> HistorySnapshot()
    {
        lock (_historySync) return _history.ToArray();
    }

    public ValuationService(
        UniversalisClient client, Configuration config,
        IGameInventory inventory, IFramework framework, IPlayerState playerState,
        RetainerService retainers,
        string configDir, IPluginLog log)
    {
        _client = client;
        _config = config;
        _inventory = inventory;
        _framework = framework;
        _playerState = playerState;
        _retainers = retainers;
        _log = log;
        Directory.CreateDirectory(configDir);
        _historyPath = Path.Combine(configDir, "history.jsonl");
        LoadHistory();
    }

    public void Start() => _loop = Task.Run(RunAsync);

    private void LoadHistory()
    {
        if (!File.Exists(_historyPath)) return;
        try
        {
            lock (_historySync)
            {
                foreach (var line in File.ReadAllLines(_historyPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var s = JsonSerializer.Deserialize<NetWorthSnapshot>(line);
                    if (s != null) _history.Add(s);
                }
                Latest = _history.LastOrDefault();
            }
        }
        catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: history load failed"); }
    }

    /// Drops the in-memory history list and re-reads history.jsonl. Called after Backup.Restore
    /// so subsequent Appends don't mix restored file contents with stale memory state.
    public void ReloadFromDisk()
    {
        lock (_fileLock) // block until any in-flight capture append completes
        {
            lock (_historySync)
            {
                _history.Clear();
                Latest = null;
            }
            LoadHistory();
        }
        Changed?.Invoke();
    }

    /// Overwrite history.jsonl with the given stream contents, holding the file lock so no capture
    /// can append in between. Called by BackupService.Import — this pathway is the only supported
    /// way to replace history.jsonl in-process. Follows with an immediate ReloadFromDisk.
    public void ImportHistoryFrom(System.IO.Stream source)
    {
        lock (_fileLock)
        {
            using var dst = File.Create(_historyPath);
            source.CopyTo(dst);
        }
        ReloadFromDisk();
    }

    private async Task RunAsync()
    {
        try { await Task.Delay(TimeSpan.FromSeconds(20), _cts.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!_cts.IsCancellationRequested)
        {
            try { await CaptureAsync(_cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: valuation capture failed"); }

            try { await Task.Delay(SnapshotInterval, _cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<NetWorthSnapshot?> CaptureAsync(CancellationToken ct)
    {
        if (!_playerState.IsLoaded) return null;

        var read = await _framework.Run(ReadInventoryOnMainThread, ct).ConfigureAwait(false);
        var retainerSnaps = _retainers.All();

        var allIds = new HashSet<uint>();
        foreach (var id in read.Bag.Keys)       allIds.Add(id);
        foreach (var id in read.Saddlebag.Keys) allIds.Add(id);
        foreach (var r in retainerSnaps)
        {
            foreach (var id in r.Inventory.Keys) allIds.Add(id);
            foreach (var l in r.Listings)        allIds.Add(l.ItemId);
        }
        allIds.Remove(0);
        allIds.Remove(1); // gil itself; not MB-tradable

        await RefreshPricesAsync(allIds, ct).ConfigureAwait(false);

        long ValueOf(IEnumerable<KeyValuePair<uint, long>> qtys)
        {
            long sum = 0;
            foreach (var kv in qtys)
                if (_priceCache.TryGetValue(kv.Key, out var pe) && pe.MinPrice > 0)
                    sum += (long)pe.MinPrice * kv.Value;
            return sum;
        }

        var bagValue = ValueOf(read.Bag);
        var sbValue  = ValueOf(read.Saddlebag);
        _log.Info("KupoTradeMaster: valuation computed — bag={Bag:N0} sb={SB:N0} priceCacheSize={Cache}", bagValue, sbValue, _priceCache.Count);

        long retainerGilTotal = 0;
        long retainerInvValue = 0;
        long listingsValue = 0;
        foreach (var r in retainerSnaps)
        {
            retainerGilTotal += r.Gil;
            retainerInvValue += ValueOf(r.Inventory);
            foreach (var l in r.Listings)
            {
                if (_priceCache.TryGetValue(l.ItemId, out var pe) && pe.MinPrice > 0)
                    listingsValue += (long)pe.MinPrice * l.Quantity;
            }
        }

        var snapshot = new NetWorthSnapshot(
            CapturedUtc: DateTime.UtcNow,
            GilOnCharacter: read.Gil,
            GilInRetainers: retainerGilTotal,
            BagValueGil: bagValue,
            SaddlebagValueGil: sbValue,
            RetainerInventoryValueGil: retainerInvValue,
            ActiveListingsValueGil: listingsValue,
            TotalGil: read.Gil + bagValue + sbValue + retainerGilTotal + retainerInvValue + listingsValue,
            UniqueItemsValued: allIds.Count(id => _priceCache.TryGetValue(id, out var pe) && pe.MinPrice > 0));

        Latest = snapshot;
        lock (_historySync) _history.Add(snapshot);

        lock (_fileLock)
        {
            try { File.AppendAllText(_historyPath, JsonSerializer.Serialize(snapshot) + "\n"); }
            catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: history append failed"); }
        }

        Changed?.Invoke();
        _log.Debug("KupoTradeMaster: net worth snapshot — cash={Cash:N0} bag={Bag:N0} sb={SB:N0} total={Tot:N0}",
            read.Gil, bagValue, sbValue, snapshot.TotalGil);
        return snapshot;
    }

    private unsafe InventoryRead ReadInventoryOnMainThread()
    {
        long gil = 0;
        var bag = new Dictionary<uint, long>();
        var sb  = new Dictionary<uint, long>();

        // Read directly from InventoryManager (the game's authoritative source) rather than IGameInventory.
        // Dalamud's IGameInventory abstraction has caching quirks where certain containers read empty until
        // the corresponding UI addon has been opened at least once.
        var mgr = InventoryManager.Instance();
        if (mgr == null)
        {
            _log.Warning("KupoTradeMaster: InventoryManager.Instance() null — skipping capture");
            return new InventoryRead(0, bag, sb);
        }

        // Gil: itemId 1 in the Currency container.
        var currency = mgr->GetInventoryContainer(InventoryType.Currency);
        if (currency != null && currency->IsLoaded)
        {
            for (var i = 0; i < currency->Size; i++)
            {
                var slot = currency->GetInventorySlot(i);
                if (slot == null) continue;
                if (slot->ItemId == 1) gil += slot->Quantity;
            }
        }

        foreach (var t in PlayerContainers) AccumulateContainer(mgr, t, bag);
        foreach (var t in SaddlebagContainers) AccumulateContainer(mgr, t, sb);

        _log.Info("KupoTradeMaster: inventory read — gil={Gil:N0} bagUniqueItems={Bag} sbUniqueItems={SB}",
            gil, bag.Count, sb.Count);
        return new InventoryRead(gil, bag, sb);
    }

    private static unsafe void AccumulateContainer(InventoryManager* mgr, InventoryType type, Dictionary<uint, long> into)
    {
        var c = mgr->GetInventoryContainer(type);
        if (c == null || !c->IsLoaded) return;
        for (var i = 0; i < c->Size; i++)
        {
            var slot = c->GetInventorySlot(i);
            if (slot == null || slot->ItemId == 0) continue;
            var id = NormalizeItemId(slot->ItemId);
            into.TryGetValue(id, out var existing);
            into[id] = existing + slot->Quantity;
        }
    }

    private static uint NormalizeItemId(uint id) => id >= 1_000_000 ? id - 1_000_000 : id;

    private async Task RefreshPricesAsync(IEnumerable<uint> ids, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var stale = ids.Where(id =>
                !_priceCache.TryGetValue(id, out var pe) ||
                (now - pe.FetchedUtc) > PriceCacheTtl)
            .ToArray();

        if (stale.Length == 0) return;

        _log.Info("KupoTradeMaster: valuation fetching {Count} items from Universalis ({Scope})", stale.Length, _config.DataScope);
        var results = await _client.FetchAsync(_config.DataScope, stale, ct).ConfigureAwait(false);

        var withListings = 0;
        var withMinPriceFallback = 0;
        foreach (var (id, item) in results)
        {
            int min;
            if (item.Listings.Count > 0)
            {
                min = item.Listings.Min(l => l.PricePerUnit);
                withListings++;
            }
            else if (item.MinPrice > 0)
            {
                // Universalis multi-item mode sometimes ships only aggregates. Trust MinPrice as fallback.
                min = item.MinPrice;
                withMinPriceFallback++;
            }
            else if (item.MinPriceNq > 0 || item.MinPriceHq > 0)
            {
                min = item.MinPriceNq > 0 ? item.MinPriceNq : item.MinPriceHq;
                withMinPriceFallback++;
            }
            else
            {
                min = 0;
            }
            _priceCache[id] = new PriceCacheEntry(min, now);
        }
        var missing = 0;
        foreach (var id in stale)
        {
            if (!results.ContainsKey(id))
            {
                _priceCache[id] = new PriceCacheEntry(0, now);
                missing++;
            }
        }

        _log.Info("KupoTradeMaster: valuation fetch — {Total} requested, {Returned} returned ({WithListings} w/ listings, {WithFallback} w/ aggregate MinPrice), {Missing} missing/untradable",
            stale.Length, results.Count, withListings, withMinPriceFallback, missing);
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* swallow */ }
        _cts.Dispose();
    }

    private readonly record struct PriceCacheEntry(int MinPrice, DateTime FetchedUtc);
    private readonly record struct InventoryRead(long Gil, Dictionary<uint, long> Bag, Dictionary<uint, long> Saddlebag);
}
