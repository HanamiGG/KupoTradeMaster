using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using KupoTradeMaster.Services;

namespace KupoTradeMaster.UI;

public sealed class WatchlistTab
{
    private readonly Plugin _plugin;
    private string _searchQuery = "";
    private (uint Id, string Name, ushort IconId)[] _searchResults = Array.Empty<(uint, string, ushort)>();
    private string _groupFilter = KupoTradeMaster.Services.GroupService.AllFilter;

    public WatchlistTab(Plugin plugin) => _plugin = plugin;

    public void Draw()
    {
        var config = _plugin.Configuration;
        var poller = _plugin.Poller;
        var taxes = _plugin.TaxRates;

        // Status strip
        ImGui.Text($"Scope: {config.DataScope}");
        ImGui.SameLine(); ImGui.TextDisabled("|");
        ImGui.SameLine();
        if (taxes.MinRatePercent.HasValue)
        {
            ImGui.Text($"Tax: {taxes.MinRatePercent}% ({taxes.MinCityName})");
            if (ImGui.IsItemHovered() && taxes.LatestByCity.Count > 0)
            {
                ImGui.BeginTooltip();
                ImGui.TextDisabled($"Live tax rates — {taxes.HomeWorldName}");
                ImGui.Separator();
                foreach (var (city, rate) in taxes.LatestByCity.OrderBy(kv => kv.Value))
                {
                    if (city == taxes.MinCityName)
                        ImGui.TextColored(new Vector4(0.55f, 1f, 0.55f, 1f), $"{city}: {rate}% (cheapest)");
                    else
                        ImGui.Text($"{city}: {rate}%");
                }
                ImGui.EndTooltip();
            }
        }
        else ImGui.TextDisabled($"Tax: {config.TaxRate * 100:0.#}% (default — waiting for login)");
        ImGui.SameLine(); ImGui.TextDisabled("|");
        ImGui.SameLine(); ImGui.Text($"Min poll: {config.PerItemMinPollSeconds}s");
        ImGui.SameLine(); ImGui.TextDisabled("|");
        ImGui.SameLine();
        ImGui.Text("Group:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140f);
        if (ImGui.BeginCombo("##ktm_wl_group", _groupFilter))
        {
            foreach (var opt in _plugin.Groups.AllFilterOptions)
            {
                var sel = opt == _groupFilter;
                if (ImGui.Selectable(opt, sel)) _groupFilter = opt;
                if (sel) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        var btnW = 120f;
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - btnW - ImGui.GetStyle().WindowPadding.X);
        if (ImGui.Button("Refresh now", new Vector2(btnW, 0))) poller.ForceRefreshAll();

        ImGui.Separator();

        // Add-item search
        DrawSearchAdd();

        ImGui.Separator();

        // Watchlist table
        var rows = BuildRows();
        rows.Sort(static (a, b) => b.RawScore.CompareTo(a.RawScore));

        const ImGuiTableFlags flags =
            ImGuiTableFlags.Reorderable | ImGuiTableFlags.Resizable |
            ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY;

        if (!ImGui.BeginTable("ktm_watchlist", 9, flags)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 60);
        ImGui.TableSetupColumn("Min listing", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Avg price", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Post-tax margin", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn("Velocity /day", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("Freshness (min)", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn("FlipScore", ImGuiTableColumnFlags.WidthFixed, 90);
        ImGui.TableSetupColumn(" ", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableHeadersRow();

        uint? removeId = null;

        foreach (var row in rows)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGuiIconExtensions.IconAndText(_plugin.TextureProviderRef, _plugin.Names.GetIconId(row.ItemId), row.Name);
            ItemContextMenu.AttachToLastItem(_plugin, row.ItemId);
            ImGui.TableNextColumn(); ImGui.TextDisabled(row.ItemId.ToString());
            ImGui.TableNextColumn(); ImGui.Text(FormatGil(row.MinPrice));
            ImGui.TableNextColumn(); ImGui.Text(FormatGil(row.AvgPrice));

            ImGui.TableNextColumn();
            if (row.MinPrice <= 0 || row.AvgPrice <= 0) ImGui.TextDisabled("—");
            else ImGui.TextColored(row.PostTaxMargin > 0
                    ? new Vector4(0.55f, 1f, 0.55f, 1f)
                    : new Vector4(1f, 0.55f, 0.55f, 1f),
                row.PostTaxMargin.ToString("N0"));

            ImGui.TableNextColumn(); ImGui.Text(row.Velocity > 0 ? row.Velocity.ToString("N2") : "—");
            ImGui.TableNextColumn(); ImGui.Text(row.FreshnessMinutes > 0 ? row.FreshnessMinutes.ToString("N0") : "—");

            ImGui.TableNextColumn();
            var normalized = FlipScorer.Normalize(row.RawScore, poller.SessionMaxRawScore);
            if (normalized <= 0) ImGui.TextDisabled("—");
            else
            {
                var color = normalized >= 66 ? new Vector4(0.35f, 1f, 0.35f, 1f)
                          : normalized >= 33 ? new Vector4(1f, 0.85f, 0.3f, 1f)
                                             : new Vector4(0.85f, 0.85f, 0.85f, 1f);
                ImGui.TextColored(color, normalized.ToString("N0"));
            }

            ImGui.TableNextColumn();
            if (ImGui.SmallButton($"x##rm{row.ItemId}"))
                removeId = row.ItemId;
        }

        ImGui.EndTable();

        if (removeId.HasValue)
        {
            lock (_plugin.Configuration.Sync)
                _plugin.Configuration.Watchlist.RemoveAll(id => id == removeId.Value);
            _plugin.Configuration.Save();
        }

        if (poller.LatestSnapshot.IsEmpty)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Waiting for first Universalis fetch (up to 30s)…");
        }
    }

    private void DrawSearchAdd()
    {
        ImGui.SetNextItemWidth(300f);
        var changed = ImGui.InputTextWithHint("##ktm_add_search", "Add item to watchlist (search by name…)", ref _searchQuery, 64);
        if (changed) _searchResults = _plugin.Names.Search(_searchQuery, 200).ToArray();

        if (_searchResults.Length == 0) return;

        ImGui.SameLine();
        ImGui.TextDisabled(_searchResults.Length >= 200
            ? "200+ matches (narrow query)"
            : $"{_searchResults.Length} match(es)");

        // Scrollable results list, capped to ~10 visible rows.
        if (ImGui.BeginTable("ktm_add_results", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY,
                new Vector2(0, 280f)))
        {
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn(" ", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableHeadersRow();

            foreach (var r in _searchResults)
            {
                var already = _plugin.Configuration.Watchlist.Contains(r.Id);
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGuiIconExtensions.IconAndText(_plugin.TextureProviderRef, r.IconId, r.Name);
                ItemContextMenu.AttachToLastItem(_plugin, r.Id);
                ImGui.TableNextColumn(); ImGui.TextDisabled(r.Id.ToString());
                ImGui.TableNextColumn();
                if (already) ImGui.TextDisabled("on list");
                else if (ImGui.SmallButton($"Add##add{r.Id}"))
                {
                    lock (_plugin.Configuration.Sync)
                        _plugin.Configuration.Watchlist.Add(r.Id);
                    _plugin.Configuration.Save();
                }
            }
            ImGui.EndTable();
        }
    }

    private List<Row> BuildRows()
    {
        var rows = new List<Row>();
        var seen = new HashSet<uint>();
        var home = _plugin.HomeWorldName;
        foreach (var id in _plugin.Groups.FilteredWatchlist(_groupFilter))
        {
            if (!seen.Add(id)) continue;

            var name = _plugin.Names.GetName(id);

            if (!_plugin.Poller.LatestSnapshot.TryGetValue(id, out var item))
            {
                rows.Add(new Row(id, name, 0, 0, 0, 0, 0, 0));
                continue;
            }

            // Min listing = your realm's cheapest listing (fall back to DC if none).
            // Post-tax margin uses the realistic sell price (home-world undercut) so a foreign
            // outlier at 2M on some other world can't inflate the number.
            var min = PriceLens.EffectiveMin(item, home);
            var avg = item.CurrentAveragePrice;
            var sellRef = PriceLens.SellReference(item, home);
            var dcMin = item.Listings.Count > 0 ? item.Listings.Min(l => (double)l.PricePerUnit) : 0;
            var velocity = item.RegularSaleVelocity;
            var freshness = item.LastUploadTimeMs > 0
                ? (DateTime.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(item.LastUploadTimeMs).UtcDateTime).TotalMinutes
                : 0;
            var postTaxMargin = (sellRef > 0 && dcMin > 0)
                ? sellRef * (1 - _plugin.Configuration.TaxRate) - dcMin
                : 0;
            var raw = _plugin.Poller.ComputeRawScore(item);

            rows.Add(new Row(id, name, min, avg, postTaxMargin, velocity, freshness, raw));
        }
        return rows;
    }

    private static string FormatGil(double gil) => gil > 0 ? gil.ToString("N0") : "—";

    private readonly record struct Row(
        uint ItemId, string Name, double MinPrice, double AvgPrice,
        double PostTaxMargin, double Velocity, double FreshnessMinutes, double RawScore);
}
