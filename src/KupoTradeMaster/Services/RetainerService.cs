using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using KupoTradeMaster.Models;

namespace KupoTradeMaster.Services;

public sealed class RetainerService : IDisposable
{
    private const string RetainerListAddon        = "RetainerList";
    private const string InventoryRetainerAddon   = "InventoryRetainer";
    private const string RetainerSellListAddon    = "RetainerSellList";
    private const string RetainerSellHistoryAddon = "RetainerSellHistory";

    private static readonly GameInventoryType[] RetainerBags =
    {
        GameInventoryType.RetainerPage1, GameInventoryType.RetainerPage2,
        GameInventoryType.RetainerPage3, GameInventoryType.RetainerPage4,
        GameInventoryType.RetainerPage5, GameInventoryType.RetainerPage6,
        GameInventoryType.RetainerPage7, GameInventoryType.RetainerCrystals,
    };

    private readonly IAddonLifecycle _lifecycle;
    private readonly IGameInventory _inventory;
    private readonly IFramework _framework;
    private readonly PluginBridge _bridge;
    private readonly IPluginLog _log;
    private readonly string _statePath;

    private readonly ConcurrentDictionary<ulong, RetainerSnapshot> _byId = new();
    private readonly object _fileLock = new();

    public event Action? Changed;

    /// Fires when a listing shrank between two captures — a probable (unconfirmed) sale.
    /// Consumers should mark journal entries created from this as estimated.
    public event Action<DetectedListingShrink>? ListingShrank;

    public sealed record DetectedListingShrink(
        ulong RetainerId, string RetainerName,
        uint ItemId, int QtyDelta, bool IsHq,
        long LastKnownUnitPrice,  // 0 if we never captured a price for this listing
        DateTime AtUtc);

    public IReadOnlyList<RetainerSnapshot> All()
        => _byId.Values.OrderByDescending(r => r.LastSeenUtc).ToArray();

    public bool TryGet(ulong id, out RetainerSnapshot snap) => _byId.TryGetValue(id, out snap!);

    public RetainerService(IAddonLifecycle lifecycle, IGameInventory inventory, IFramework framework,
                           PluginBridge bridge, string configDir, IPluginLog log)
    {
        _lifecycle = lifecycle;
        _inventory = inventory;
        _framework = framework;
        _bridge = bridge;
        _log = log;
        Directory.CreateDirectory(configDir);
        _statePath = Path.Combine(configDir, "retainers.json");
        Load();
        SeedFromAllaganTools();

        _lifecycle.RegisterListener(AddonEvent.PostSetup, RetainerListAddon, OnRetainerListOpen);
        _lifecycle.RegisterListener(AddonEvent.PostRefresh, RetainerListAddon, OnRetainerListOpen);

        // Every retainer-detail screen implies a specific retainer is currently active; sample on both open and refresh
        // so we catch the addon before AND after the game has populated its data arrays.
        foreach (var addon in new[] { InventoryRetainerAddon, RetainerSellListAddon, RetainerSellHistoryAddon })
        {
            _lifecycle.RegisterListener(AddonEvent.PostSetup,   addon, OnRetainerScreenActivity);
            _lifecycle.RegisterListener(AddonEvent.PostRefresh, addon, OnRetainerScreenActivity);
        }
    }

    /// Force-capture the currently active retainer right now (called by the Refresh button in the UI).
    public void RefreshActive() => _ = _framework.RunOnFrameworkThread(CaptureActiveRetainerInventory);

    private void Load()
    {
        if (!File.Exists(_statePath)) return;
        try
        {
            var json = File.ReadAllText(_statePath);
            var list = JsonSerializer.Deserialize<List<RetainerSnapshot>>(json);
            if (list == null) return;
            foreach (var r in list) _byId[r.RetainerId] = r;
            _log.Info("KupoTradeMaster: loaded {N} retainer snapshots", list.Count);
        }
        catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: retainer load failed"); }
    }

    /// Drops the in-memory retainer dictionary and re-reads retainers.json. Called after Backup.Restore.
    public void ReloadFromDisk()
    {
        lock (_fileLock)
        {
            _byId.Clear();
            Load();
        }
        Changed?.Invoke();
    }

    /// Overwrite retainers.json with the given stream contents, holding the file lock so no
    /// concurrent Save from a live retainer capture can interleave.
    public void ImportFrom(System.IO.Stream source)
    {
        lock (_fileLock)
        {
            using var dst = File.Create(_statePath);
            source.CopyTo(dst);
        }
        ReloadFromDisk();
    }

    private void Save()
    {
        lock (_fileLock)
        {
            try { File.WriteAllText(_statePath, JsonSerializer.Serialize(_byId.Values.ToList())); }
            catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: retainer save failed"); }
        }
    }

    /// If Allagan Tools is installed and initialized, use its roster to add placeholder snapshots for retainers
    /// we haven't visited this session — so they show up in the Retainers tab as "known but not yet captured."
    public void SeedFromAllaganTools()
    {
        if (!_bridge.HasAllaganTools || !_bridge.AllaganToolsReady()) return;
        var owned = _bridge.AllaganToolsCharactersOwnedByActive();
        if (owned == null) return;

        var added = 0;
        foreach (var cid in owned)
        {
            if (cid == 0) continue;
            if (_byId.ContainsKey(cid)) continue;
            _byId[cid] = new RetainerSnapshot(
                RetainerId: cid,
                Name: $"#{cid:X}",
                Gil: 0,
                Inventory: new Dictionary<uint, long>(),
                Listings: new List<RetainerListingSnapshot>(),
                LastSeenUtc: DateTime.MinValue);
            added++;
        }
        if (added > 0)
        {
            Save();
            Changed?.Invoke();
            _log.Info("KupoTradeMaster: seeded {N} retainer placeholders from Allagan Tools roster", added);
        }
    }

    /// Refresh names + gil from AutoRetainer offline data (populated even when the retainer isn't summoned).
    /// Only updates fields — does not overwrite captured inventory/listings.
    public void SyncFromAutoRetainer()
    {
        if (!_bridge.HasAutoRetainer) return;
        var offline = _bridge.AutoRetainerRetainers();
        if (offline.Count == 0) return;

        var updated = 0;
        foreach (var r in offline)
        {
            if (r.RetainerId == 0) continue;
            if (_byId.TryGetValue(r.RetainerId, out var prev))
            {
                _byId[r.RetainerId] = prev with
                {
                    Name = string.IsNullOrWhiteSpace(prev.Name) || prev.Name.StartsWith('#') ? r.Name : prev.Name,
                    Gil = r.Gil > 0 ? r.Gil : prev.Gil,
                };
            }
            else
            {
                _byId[r.RetainerId] = new RetainerSnapshot(
                    RetainerId: r.RetainerId,
                    Name: r.Name,
                    Gil: r.Gil,
                    Inventory: new Dictionary<uint, long>(),
                    Listings: new List<RetainerListingSnapshot>(),
                    LastSeenUtc: DateTime.MinValue);
            }
            updated++;
        }
        if (updated > 0)
        {
            Save();
            Changed?.Invoke();
            _log.Info("KupoTradeMaster: synced {N} retainers from AutoRetainer offline data", updated);
        }
    }

    private unsafe void OnRetainerListOpen(AddonEvent type, AddonArgs args)
    {
        try
        {
            var mgr = RetainerManager.Instance();
            if (mgr == null) return;

            var count = 0;
            var retainers = mgr->Retainers;
            for (var i = 0; i < retainers.Length; i++)
            {
                var r = retainers[i];
                if (r.RetainerId == 0) continue;

                var name = r.NameString ?? $"#{r.RetainerId}";
                var existing = _byId.TryGetValue(r.RetainerId, out var prev) ? prev : null;

                // If the Retainer struct's Gil field reads 0 at the bell (unsummoned), keep the last known value.
                var gilFromStruct = (long)r.Gil;
                var gil = gilFromStruct > 0 ? gilFromStruct : (existing?.Gil ?? 0);

                _byId[r.RetainerId] = new RetainerSnapshot(
                    RetainerId: r.RetainerId,
                    Name: name,
                    Gil: gil,
                    Inventory: existing?.Inventory ?? new Dictionary<uint, long>(),
                    Listings: existing?.Listings ?? new List<RetainerListingSnapshot>(),
                    LastSeenUtc: DateTime.UtcNow);
                count++;
            }
            Save();
            Changed?.Invoke();
            _log.Debug("KupoTradeMaster: RetainerList open — snapshotted {N} retainers", count);
        }
        catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: OnRetainerListOpen failed"); }
    }

    private void OnRetainerScreenActivity(AddonEvent type, AddonArgs args)
    {
        _ = _framework.RunOnFrameworkThread(CaptureActiveRetainerInventory);
    }

    private unsafe void CaptureActiveRetainerInventory()
    {
        try
        {
            var mgr = RetainerManager.Instance();
            if (mgr == null) return;

            var active = mgr->GetActiveRetainer();
            if (active == null || active->RetainerId == 0) return;

            var activeId = active->RetainerId;
            var activeName = active->NameString ?? $"#{activeId}";
            // The Retainer struct's Gil field is populated only when the retainer is actively summoned; when the
            // bell UI is open but no retainer is picked, the field can read stale. When active, this is authoritative.
            var activeGil = (long)active->Gil;

            var inventory = new Dictionary<uint, long>();
            foreach (var t in RetainerBags)
            {
                foreach (var it in _inventory.GetInventoryItems(t))
                {
                    if (it.IsEmpty || it.BaseItemId == 0) continue;
                    inventory.TryGetValue(it.BaseItemId, out var existing);
                    inventory[it.BaseItemId] = existing + it.Quantity;
                }
            }

            var listings = new List<RetainerListingSnapshot>();
            // InventoryManager.RetainerMarketPrices is a fixed-size array indexed by the market slot;
            // it holds the retainer's own listing unit price per slot. Only populated while the retainer
            // is summoned and the RetainerSellList / RetainerSell screen has been touched at least once.
            var invMgr = InventoryManager.Instance();
            var marketPrices = invMgr != null ? invMgr->RetainerMarketPrices : default;

            foreach (var it in _inventory.GetInventoryItems(GameInventoryType.RetainerMarket))
            {
                if (it.IsEmpty || it.BaseItemId == 0) continue;
                var slot = (int)it.InventorySlot;
                long unitPrice = 0;
                if (slot >= 0 && slot < marketPrices.Length)
                    unitPrice = (long)marketPrices[slot];

                listings.Add(new RetainerListingSnapshot(
                    ItemId: it.BaseItemId,
                    Quantity: it.Quantity,
                    IsHq: it.IsHq,
                    UnitPriceGil: unitPrice,
                    LastSeenUtc: DateTime.UtcNow));
            }

            // If RetainerMarketPrices hasn't been populated yet on this capture (bag view only, sell list not
            // touched), preserve last-known prices from the previous snapshot rather than persisting zeros.
            if (_byId.TryGetValue(activeId, out var previous))
            {
                var prevPricesByKey = previous.Listings
                    .Where(l => l.UnitPriceGil > 0)
                    .GroupBy(l => (l.ItemId, l.IsHq))
                    .ToDictionary(g => g.Key, g => g.First().UnitPriceGil);
                for (var i = 0; i < listings.Count; i++)
                {
                    if (listings[i].UnitPriceGil > 0) continue;
                    if (prevPricesByKey.TryGetValue((listings[i].ItemId, listings[i].IsHq), out var fallback))
                        listings[i] = listings[i] with { UnitPriceGil = fallback };
                }

                // Diff-based sale detection: any listing whose qty shrank vs last capture is a probable sale.
                // False positive: user cancelled a listing (moves item back to bag).
                if (previous.Listings.Count > 0)
                {
                    var prevByKey = previous.Listings
                        .GroupBy(l => (l.ItemId, l.IsHq))
                        .ToDictionary(g => g.Key, g => (
                            Qty: g.Sum(l => l.Quantity),
                            Price: g.Where(l => l.UnitPriceGil > 0).Select(l => l.UnitPriceGil).FirstOrDefault()));
                    var newByKey = listings
                        .GroupBy(l => (l.ItemId, l.IsHq))
                        .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

                    foreach (var kv in prevByKey)
                    {
                        newByKey.TryGetValue(kv.Key, out var newQty);
                        var delta = kv.Value.Qty - newQty;
                        if (delta > 0)
                        {
                            ListingShrank?.Invoke(new DetectedListingShrink(
                                RetainerId: activeId,
                                RetainerName: activeName,
                                ItemId: kv.Key.ItemId,
                                QtyDelta: delta,
                                IsHq: kv.Key.IsHq,
                                LastKnownUnitPrice: kv.Value.Price,
                                AtUtc: DateTime.UtcNow));
                        }
                    }
                }
            }

            _byId[activeId] = new RetainerSnapshot(
                RetainerId: activeId,
                Name: activeName,
                Gil: activeGil,
                Inventory: inventory,
                Listings: listings,
                LastSeenUtc: DateTime.UtcNow);

            Save();
            Changed?.Invoke();
            _log.Debug("KupoTradeMaster: {Name} inventory captured — {N} items, {M} listings",
                activeName, inventory.Count, listings.Count);
        }
        catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: CaptureActiveRetainerInventory failed"); }
    }

    public void Dispose()
    {
        _lifecycle.UnregisterListener(OnRetainerListOpen);
        _lifecycle.UnregisterListener(OnRetainerScreenActivity);
    }
}
