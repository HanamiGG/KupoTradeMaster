using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

namespace KupoTradeMaster.Services;

/// Watches the poller snapshot; when any watchlist item's min listing crosses at-or-below its
/// buy-formula threshold, prints a one-time chat message (until the item goes above the threshold
/// again). Uses whichever formula the item's owning group prescribes, falling back to the global one.
public sealed class BuySignalWatcher : IDisposable
{
    private readonly Plugin _plugin;
    private readonly WatchlistPoller _poller;
    private readonly Configuration _config;
    private readonly GroupService _groups;
    private readonly ItemNameProvider _names;
    private readonly IChatGui _chat;
    private readonly IPluginLog _log;

    private readonly ConcurrentDictionary<uint, bool> _lastFlagged = new();
    private readonly ConcurrentDictionary<string, PriceDsl.INode> _compiledCache = new();

    public BuySignalWatcher(Plugin plugin, WatchlistPoller poller, Configuration config,
                            GroupService groups, ItemNameProvider names, IChatGui chat, IPluginLog log)
    {
        _plugin = plugin;
        _poller = poller;
        _config = config;
        _groups = groups;
        _names = names;
        _chat = chat;
        _log = log;
        _poller.SnapshotChanged += OnSnapshotChanged;
    }

    private void OnSnapshotChanged()
    {
        if (!_config.BuySignalChatNotifications) return;

        // Snapshot both dictionaries to arrays BEFORE iterating — UI thread mutates them via context
        // menus (Add/Remove watchlist item, Mute alerts). Concurrent modification during enumeration
        // throws InvalidOperationException and kills the watcher for the session.
        uint[] watchIds;
        Dictionary<uint, DateTime> mutes;
        lock (_config.Sync)
        {
            watchIds = _config.Watchlist.Distinct().ToArray();
            mutes = new Dictionary<uint, DateTime>(_config.AlertMutedUntilUtc);
        }

        var home = _plugin.HomeWorldName;
        var now = DateTime.UtcNow;

        foreach (var id in watchIds)
        {
            if (!_poller.LatestSnapshot.TryGetValue(id, out var item)) continue;
            if (item.Listings.Count == 0) continue;

            var groupsFor = _groups.GroupsContaining(id).ToArray();
            var formulaSrc = groupsFor.Length > 0
                ? _groups.GetFormula(groupsFor[0])  // if in multiple groups, use the first alphabetically
                : _config.BuyFormula;
            if (string.IsNullOrWhiteSpace(formulaSrc)) { _lastFlagged.TryRemove(id, out _); continue; }

            var node = Compile(formulaSrc);
            if (node == null) continue;

            // Trigger price = home world's min listing (that's what YOU'D actually buy at); DC-min
            // available in DSL via mbmin/dbmarket for power users who want cross-world triggers.
            var effMin = PriceLens.EffectiveMin(item, home);
            if (effMin <= 0) continue;
            var dcMin = item.Listings.Min(l => (double)l.PricePerUnit);
            var ctx = new PriceDslContext(
                ItemId: id,
                MinPrice: dcMin,
                AvgPrice: item.CurrentAveragePrice,
                Velocity: item.RegularSaleVelocity,
                Vendor: _names.GetVendorPrice(id),
                TaxRate: _config.TaxRate,
                MinPriceHw: PriceLens.HomeWorldMin(item, home),
                AvgPriceHw: PriceLens.HomeWorldAvg(item, home));

            double threshold;
            try { threshold = node.Eval(ctx); } catch { continue; }
            if (threshold <= 0) { _lastFlagged.TryRemove(id, out _); continue; }

            var flagged = effMin <= threshold;
            var was = _lastFlagged.TryGetValue(id, out var prev) && prev;
            _lastFlagged[id] = flagged;

            if (flagged && !was)
            {
                // Honor per-item mute cooldown before printing. Expired mutes get GC'd on config load.
                if (mutes.TryGetValue(id, out var muteUntil) && muteUntil > now) continue;

                var name = _names.GetName(id);
                var msg = $"[KupoTradeMaster] {name} is at {effMin:N0}g — under {threshold:N0}g threshold";
                try { _chat.Print(new XivChatEntry { Message = msg, Type = XivChatType.Notice }); }
                catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: chat print failed"); }
            }
        }
    }

    private PriceDsl.INode? Compile(string src)
    {
        var customs = _plugin.CompiledCustomSources();
        // Key must include a content fingerprint of the custom-sources dict — using .Count would
        // hit-cache a stale compilation after a user edits a source's formula body.
        var key = src + "#" + _plugin.CustomSourcesFingerprint;
        if (_compiledCache.TryGetValue(key, out var cached)) return cached;
        if (PriceDsl.TryCompile(src, customs, out var node, out _) && node != null)
        {
            _compiledCache[key] = node;
            return node;
        }
        return null;
    }

    public void Dispose() => _poller.SnapshotChanged -= OnSnapshotChanged;
}
