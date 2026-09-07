using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using KupoTradeMaster.Services;

namespace KupoTradeMaster.UI;

/// The "what to flip right now" one-glance view — OSRS Flipping Utilities-style sniper feed
/// over the current watchlist. Ranked by post-tax margin × sqrt(velocity), showing only items
/// where the current min listing is below the rolling average price (positive-margin buys).
public sealed class BestDealsTab
{
    private readonly Plugin _plugin;
    private string _groupFilter = KupoTradeMaster.Services.GroupService.AllFilter;
    private string _compiledFor = "";
    private PriceDsl.INode? _compiled;

    public BestDealsTab(Plugin plugin) => _plugin = plugin;

    private PriceDsl.INode? CurrentFormula()
    {
        var src = _plugin.Groups.GetFormula(_groupFilter);
        if (string.IsNullOrWhiteSpace(src)) return null;
        var customs = _plugin.CompiledCustomSources();
        // Cache key must include a content fingerprint of the custom-sources dict — using .Count
        // would silently hit a stale compilation after a user edits a source's formula body.
        var key = src + "#" + _plugin.CustomSourcesFingerprint;
        if (key != _compiledFor)
        {
            PriceDsl.TryCompile(src, customs, out _compiled, out _);
            _compiledFor = key;
        }
        return _compiled;
    }

    public void Draw()
    {
        var poller = _plugin.Poller;
        var cfg = _plugin.Configuration;

        var deals = BuildDeals().OrderByDescending(d => d.RawScore).ToList();

        ImGui.Text($"{deals.Count} positive-margin opportunity(ies)");
        ImGui.SameLine();
        ImGui.TextDisabled($"(scope: {cfg.DataScope}   tax: {cfg.TaxRate * 100:0.#}%)");
        ImGui.SameLine();
        ImGui.TextDisabled("|");
        ImGui.SameLine();
        ImGui.Text("Group:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140f);
        if (ImGui.BeginCombo("##ktm_bd_group", _groupFilter))
        {
            foreach (var opt in _plugin.Groups.AllFilterOptions)
            {
                var sel = opt == _groupFilter;
                if (ImGui.Selectable(opt, sel)) _groupFilter = opt;
                if (sel) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.Separator();

        if (deals.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Waiting for prices — or your watchlist has no positive-margin items right now.");
            ImGui.TextDisabled("Add more items via the Watchlist tab to widen the search.");
            return;
        }

        const ImGuiTableFlags flags = ImGuiTableFlags.Reorderable | ImGuiTableFlags.Resizable |
                                       ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY;

        var formula = CurrentFormula();
        var cols = formula != null ? 9 : 8;

        if (!ImGui.BeginTable("ktm_best_deals", cols, flags)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("Buy at", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("Cheapest world", ImGuiTableColumnFlags.WidthFixed, 160);
        ImGui.TableSetupColumn("Sell at ~", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("Post-tax /unit", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn("Velocity /day", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("Freshness (min)", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn("FlipScore", ImGuiTableColumnFlags.WidthFixed, 90);
        if (formula != null) ImGui.TableSetupColumn("Buy under", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableHeadersRow();

        foreach (var d in deals)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiIconExtensions.IconAndText(_plugin.TextureProviderRef, _plugin.Names.GetIconId(d.ItemId), d.Name);
            ItemContextMenu.AttachToLastItem(_plugin, d.ItemId);

            ImGui.TableNextColumn(); ImGui.Text(d.MinListing.ToString("N0"));

            ImGui.TableNextColumn();
            if (d.CheapestWorld != null)
            {
                ImGui.Text($"{d.CheapestWorld} @ {d.CheapestWorldPrice:N0}");
                if (ImGui.IsItemHovered() && d.PerWorld.Count > 1)
                {
                    ImGui.BeginTooltip();
                    ImGui.TextDisabled("All worlds (min price / units listed)");
                    ImGui.Separator();
                    foreach (var w in d.PerWorld.Take(12))
                        ImGui.Text($"{w.World,-14} {w.MinPrice,9:N0}   ({w.TotalUnits}u)");
                    ImGui.EndTooltip();
                }
            }
            else ImGui.TextDisabled("—");

            ImGui.TableNextColumn(); ImGui.Text(d.SellReference.ToString("N0"));

            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(0.55f, 1f, 0.55f, 1f), d.PostTaxPerUnit.ToString("N0"));

            ImGui.TableNextColumn(); ImGui.Text(d.Velocity.ToString("N2"));
            ImGui.TableNextColumn(); ImGui.Text(d.FreshnessMinutes.ToString("N0"));

            ImGui.TableNextColumn();
            var normalized = FlipScorer.Normalize(d.RawScore, poller.SessionMaxRawScore);
            var scoreColor = normalized >= 66 ? new Vector4(0.35f, 1f, 0.35f, 1f)
                           : normalized >= 33 ? new Vector4(1f, 0.85f, 0.3f, 1f)
                                              : new Vector4(0.85f, 0.85f, 0.85f, 1f);
            ImGui.TextColored(scoreColor, normalized.ToString("N0"));

            if (formula != null)
            {
                ImGui.TableNextColumn();
                var snap = _plugin.Poller.LatestSnapshot.TryGetValue(d.ItemId, out var item) ? item : null;
                var home = _plugin.HomeWorldName;
                var ctx = new PriceDslContext(
                    ItemId: d.ItemId,
                    MinPrice: d.MinListing,
                    AvgPrice: snap?.CurrentAveragePrice ?? d.SellReference,
                    Velocity: d.Velocity,
                    Vendor: _plugin.Names.GetVendorPrice(d.ItemId),
                    TaxRate: cfg.TaxRate,
                    MinPriceHw: snap != null ? PriceLens.HomeWorldMin(snap, home) : 0,
                    AvgPriceHw: snap != null ? PriceLens.HomeWorldAvg(snap, home) : 0);
                double threshold = 0;
                try { threshold = formula.Eval(ctx); } catch { threshold = 0; }
                if (threshold > 0)
                {
                    var hit = d.MinListing <= threshold;
                    var color = hit ? new Vector4(0.35f, 1f, 0.35f, 1f) : new Vector4(1f, 0.55f, 0.55f, 1f);
                    ImGui.TextColored(color, hit ? $"≤ {threshold:N0} ✓" : $"{threshold:N0}");
                }
                else ImGui.TextDisabled("—");
            }
        }
        ImGui.EndTable();
    }

    private IEnumerable<Deal> BuildDeals()
    {
        var poller = _plugin.Poller;
        var config = _plugin.Configuration;
        var home = _plugin.HomeWorldName;
        foreach (var id in _plugin.Groups.FilteredWatchlist(_groupFilter))
        {
            if (!poller.LatestSnapshot.TryGetValue(id, out var item)) continue;
            if (item.Listings.Count == 0) continue;

            var perWorld = CrossWorldAnalyzer.PerWorldMins(item);
            string? cheapWorld = perWorld.Count > 0 ? perWorld[0].World : null;
            int cheapPrice = perWorld.Count > 0 ? perWorld[0].MinPrice : 0;
            if (cheapPrice <= 0) continue;

            // Sell reference is what YOU'LL realistically get on your home world (undercut by 1g),
            // NOT the DC-wide currentAveragePrice — that lets a foreign outlier inflate the margin.
            var sellRef = PriceLens.SellReference(item, home);
            if (sellRef <= 0) continue;
            var postTax = sellRef * (1 - config.TaxRate) - cheapPrice;
            if (postTax <= 0) continue;

            var freshness = item.LastUploadTimeMs > 0
                ? (DateTime.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(item.LastUploadTimeMs).UtcDateTime).TotalMinutes
                : 0;
            var raw = poller.ComputeRawScore(item);

            yield return new Deal(
                ItemId: id,
                Name: _plugin.Names.GetName(id),
                MinListing: cheapPrice,
                SellReference: sellRef,
                PostTaxPerUnit: postTax,
                Velocity: item.RegularSaleVelocity,
                FreshnessMinutes: freshness,
                RawScore: raw,
                CheapestWorld: cheapWorld,
                CheapestWorldPrice: cheapPrice,
                PerWorld: perWorld);
        }
    }

    private readonly record struct Deal(
        uint ItemId, string Name,
        double MinListing, double SellReference, double PostTaxPerUnit,
        double Velocity, double FreshnessMinutes, double RawScore,
        string? CheapestWorld, int CheapestWorldPrice,
        IReadOnlyList<(string World, int MinPrice, int TotalUnits)> PerWorld);
}
