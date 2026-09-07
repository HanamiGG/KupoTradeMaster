using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using KupoTradeMaster.Services;

namespace KupoTradeMaster.UI;

/// Cross-world arbitrage lens: for each watchlist item, find the cheapest world within the current
/// data scope, compute expected profit if you buy there and re-list on your own retainers at DC avg.
/// Answers "which items are worth traveling for right now".
public sealed class ArbitrageTab
{
    private readonly Plugin _plugin;
    private string _groupFilter = GroupService.AllFilter;
    private int _minMarginPct = 15;

    public ArbitrageTab(Plugin plugin) => _plugin = plugin;

    public void Draw()
    {
        var cfg = _plugin.Configuration;
        var poller = _plugin.Poller;

        ImGui.Text("Cross-world arbitrage");
        ImGui.SameLine(); ImGui.TextDisabled($"(scope: {cfg.DataScope}   tax: {cfg.TaxRate * 100:0.#}%)");
        ImGui.SameLine(); ImGui.TextDisabled("|");
        ImGui.SameLine(); ImGui.Text("Group:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140f);
        if (ImGui.BeginCombo("##ktm_arb_group", _groupFilter))
        {
            foreach (var opt in _plugin.Groups.AllFilterOptions)
            {
                var sel = opt == _groupFilter;
                if (ImGui.Selectable(opt, sel)) _groupFilter = opt;
                if (sel) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine(); ImGui.TextDisabled("|");
        ImGui.SameLine(); ImGui.Text("Min margin:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f);
        ImGui.SliderInt("##ktm_arb_margin", ref _minMarginPct, 5, 100, "%d%%");

        ImGui.Separator();

        var opportunities = BuildOpportunities().Where(o => o.MarginPct >= _minMarginPct)
                                                 .OrderByDescending(o => o.PostTaxProfitPerUnit)
                                                 .ToList();

        ImGui.Text($"{opportunities.Count} opportunity(ies) at ≥ {_minMarginPct}% margin");
        ImGui.Spacing();

        if (opportunities.Count == 0)
        {
            ImGui.TextDisabled("No positive-margin cross-world arbitrage found. Add more items to Watchlist, widen the scope, or lower the margin threshold.");
            return;
        }

        const ImGuiTableFlags flags = ImGuiTableFlags.Reorderable | ImGuiTableFlags.Resizable |
                                       ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY;
        if (!ImGui.BeginTable("ktm_arb", 8, flags)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("Buy on (world @ price)", ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableSetupColumn("Units", ImGuiTableColumnFlags.WidthFixed, 60);
        ImGui.TableSetupColumn("Sell ~ (DC avg)", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn("Post-tax /unit", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn("Margin %", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Velocity /day", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("Est. profit (all)", ImGuiTableColumnFlags.WidthFixed, 140);
        ImGui.TableHeadersRow();

        foreach (var o in opportunities)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiIconExtensions.IconAndText(_plugin.TextureProviderRef, _plugin.Names.GetIconId(o.ItemId), o.Name);
            ItemContextMenu.AttachToLastItem(_plugin, o.ItemId);

            ImGui.TableNextColumn(); ImGui.Text($"{o.BuyWorld} @ {o.BuyPrice:N0}");
            if (ImGui.IsItemHovered() && o.PerWorld.Count > 1)
            {
                ImGui.BeginTooltip();
                ImGui.TextDisabled("All worlds (min price / units listed)");
                ImGui.Separator();
                foreach (var w in o.PerWorld.Take(12))
                    ImGui.Text($"{w.World,-14} {w.MinPrice,9:N0}   ({w.TotalUnits}u)");
                ImGui.EndTooltip();
            }
            ImGui.TableNextColumn(); ImGui.Text(o.UnitsAvailable.ToString("N0"));
            ImGui.TableNextColumn(); ImGui.Text(o.SellReference.ToString("N0"));
            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(0.55f, 1f, 0.55f, 1f), o.PostTaxProfitPerUnit.ToString("N0"));
            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.4f, 1f), $"{o.MarginPct:N0}%");
            ImGui.TableNextColumn(); ImGui.Text(o.Velocity.ToString("N2"));
            ImGui.TableNextColumn();
            ImGui.Text((o.PostTaxProfitPerUnit * o.UnitsAvailable).ToString("N0"));
        }
        ImGui.EndTable();
    }

    private IEnumerable<Opportunity> BuildOpportunities()
    {
        var cfg = _plugin.Configuration;
        foreach (var id in _plugin.Groups.FilteredWatchlist(_groupFilter))
        {
            if (!_plugin.Poller.LatestSnapshot.TryGetValue(id, out var item)) continue;
            if (item.CurrentAveragePrice <= 0) continue;

            var perWorld = CrossWorldAnalyzer.PerWorldMins(item);
            if (perWorld.Count == 0) continue;

            var cheapest = perWorld[0];
            var sellRef = item.CurrentAveragePrice;
            var postTaxProfit = sellRef * (1 - cfg.TaxRate) - cheapest.MinPrice;
            if (postTaxProfit <= 0) continue;
            var marginPct = postTaxProfit / cheapest.MinPrice * 100.0;

            yield return new Opportunity(
                ItemId: id,
                Name: _plugin.Names.GetName(id),
                BuyWorld: cheapest.World,
                BuyPrice: cheapest.MinPrice,
                UnitsAvailable: cheapest.TotalUnits,
                SellReference: sellRef,
                PostTaxProfitPerUnit: postTaxProfit,
                MarginPct: marginPct,
                Velocity: item.RegularSaleVelocity,
                PerWorld: perWorld);
        }
    }

    private readonly record struct Opportunity(
        uint ItemId, string Name,
        string BuyWorld, int BuyPrice, int UnitsAvailable,
        double SellReference, double PostTaxProfitPerUnit, double MarginPct,
        double Velocity,
        IReadOnlyList<(string World, int MinPrice, int TotalUnits)> PerWorld);
}
