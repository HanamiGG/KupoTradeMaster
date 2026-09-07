using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using KupoTradeMaster.Models;
using KupoTradeMaster.Services;

namespace KupoTradeMaster.UI;

public sealed class RetainersTab
{
    private readonly Plugin _plugin;
    private ulong _selectedRetainer;

    public RetainersTab(Plugin plugin) => _plugin = plugin;

    private void DrawIntegrationBadges()
    {
        var b = _plugin.Bridge;
        Badge("Allagan Tools", b.HasAllaganTools, b.HasAllaganTools && b.AllaganToolsReady()
            ? "roster seeded"
            : (b.HasAllaganTools ? "detected — awaiting init" : "not installed"));
        ImGui.SameLine();
        Badge("AutoRetainer", b.HasAutoRetainer, b.HasAutoRetainer
            ? "detected — pulling offline retainer gil"
            : "not installed — offline retainer gil requires it");
        ImGui.SameLine();
        Badge("Artisan", b.HasArtisan, b.HasArtisan
            ? "detected (used by future Crafting tab)"
            : "not installed");
    }

    private static void Badge(string label, bool on, string tooltip)
    {
        var colOn  = new Vector4(0.35f, 1f, 0.55f, 1f);
        var colOff = new Vector4(0.55f, 0.55f, 0.55f, 1f);
        ImGui.PushStyleColor(ImGuiCol.Text, on ? colOn : colOff);
        ImGui.Text(on ? $"● {label}" : $"○ {label}");
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(tooltip);
            ImGui.EndTooltip();
        }
    }

    public void Draw()
    {
        // Cheap re-check so newly-loaded plugins light up their badges without a plugin restart.
        _plugin.Bridge.Rescan();
        if (_plugin.Bridge.HasAllaganTools) _plugin.Retainers.SeedFromAllaganTools();
        if (_plugin.Bridge.HasAutoRetainer) _plugin.Retainers.SyncFromAutoRetainer();

        var retainers = _plugin.Retainers.All();
        if (retainers.Count == 0)
        {
            ImGui.TextDisabled("No retainer snapshots yet. Talk to a Summoning Bell — KupoTradeMaster will capture each retainer's inventory and listings as you visit them.");
            return;
        }

        ImGui.Text($"{retainers.Count} retainer(s) tracked");
        ImGui.SameLine();
        DrawIntegrationBadges();
        ImGui.SameLine();
        var btnW = 180f;
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - btnW - ImGui.GetStyle().WindowPadding.X);
        if (ImGui.Button("Capture active retainer", new Vector2(btnW, 0)))
            _plugin.Retainers.RefreshActive();
        ImGui.Separator();

        var avail = ImGui.GetContentRegionAvail();
        var leftW = MathF.Max(220f, avail.X * 0.28f);

        // Left: retainer list
        ImGui.BeginChild("ktm_retainer_list", new Vector2(leftW, 0), true);
        foreach (var r in retainers)
        {
            var label = $"{r.Name}\n{r.Gil:N0} gil  •  {r.Listings.Count} listing(s)";
            if (ImGui.Selectable(label + "###" + r.RetainerId, _selectedRetainer == r.RetainerId,
                                 ImGuiSelectableFlags.None, new Vector2(0, 44)))
                _selectedRetainer = r.RetainerId;
            ImGui.Spacing();
        }
        ImGui.EndChild();

        ImGui.SameLine();

        // Right: selected retainer detail
        ImGui.BeginChild("ktm_retainer_detail", new Vector2(0, 0), true);
        if (_selectedRetainer == 0 || !_plugin.Retainers.TryGet(_selectedRetainer, out var sel))
            sel = retainers[0];

        DrawRetainerDetail(sel);
        ImGui.EndChild();
    }

    private void DrawRetainerDetail(RetainerSnapshot r)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.95f, 0.7f, 1f));
        ImGui.SetWindowFontScale(1.3f);
        ImGui.Text(r.Name);
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopStyleColor();

        var seenAgo = (DateTime.UtcNow - r.LastSeenUtc).TotalMinutes;
        var seenLabel = seenAgo < 1 ? "just now"
                      : seenAgo < 60 ? $"{seenAgo:N0} min ago"
                      : seenAgo < 60 * 24 ? $"{seenAgo / 60:N1} hr ago"
                      : $"{seenAgo / (60 * 24):N1} d ago";
        ImGui.TextDisabled($"last seen {seenLabel}");

        ImGui.Text($"Gil: {r.Gil:N0}");
        ImGui.SameLine(); ImGui.TextDisabled("|");
        ImGui.SameLine(); ImGui.Text($"Inventory items: {r.Inventory.Count}");
        ImGui.SameLine(); ImGui.TextDisabled("|");
        ImGui.SameLine(); ImGui.Text($"Active listings: {r.Listings.Count}/20");

        // AutoRetainer offline enrichment: venture status, level, job.
        if (_plugin.Bridge.HasAutoRetainer)
        {
            var arRow = _plugin.Bridge.AutoRetainerRetainers().FirstOrDefault(x => x.RetainerId == r.RetainerId);
            if (arRow != null)
            {
                if (arRow.HasVenture && arRow.VentureEndsAtUnix > 0)
                {
                    var remaining = DateTimeOffset.FromUnixTimeSeconds(arRow.VentureEndsAtUnix) - DateTimeOffset.UtcNow;
                    var ventureLabel = remaining.TotalSeconds <= 0
                        ? "READY"
                        : remaining.TotalMinutes < 60
                            ? $"in {remaining.TotalMinutes:N0} min"
                            : $"in {remaining.TotalHours:N1} hr";
                    var color = remaining.TotalSeconds <= 0
                        ? new Vector4(0.55f, 1f, 0.55f, 1f)
                        : new Vector4(0.9f, 0.9f, 0.9f, 1f);
                    ImGui.TextColored(color, $"Venture #{arRow.VentureId} — {ventureLabel}");
                }
                else if (arRow.Level > 0)
                {
                    ImGui.TextDisabled($"Level {arRow.Level} · no active venture");
                }
                ImGui.TextDisabled($"via AutoRetainer offline data");
            }
        }

        ImGui.Separator();

        // Active listings
        ImGui.Text("Active market listings");
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders |
                                      ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY;

        var half = ImGui.GetContentRegionAvail().Y * 0.45f;

        if (ImGui.BeginTable("ktm_retainer_listings", 6, flags, new Vector2(0, half)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("Your unit price", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableSetupColumn("MB min (home)", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableSetupColumn("vs market", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableHeadersRow();

            var home = _plugin.HomeWorldName;
            foreach (var l in r.Listings.OrderByDescending(x => x.Quantity))
            {
                // Prefer home-world lowest listing (that's who will undercut this listing); if we've
                // never polled this item, fall back to the DC-wide cache — flagged with an asterisk.
                var mbMin = 0;
                var sourcedFromHome = false;
                if (_plugin.Poller.LatestSnapshot.TryGetValue(l.ItemId, out var snap))
                {
                    mbMin = PriceLens.EffectiveMin(snap, home);
                    sourcedFromHome = PriceLens.IsHomeWorldSourced(snap, home);
                }
                if (mbMin == 0)
                    mbMin = (int)Math.Min(int.MaxValue, _plugin.Valuation.GetCachedMinPrice(l.ItemId));

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGuiIconExtensions.IconAndText(_plugin.TextureProviderRef, _plugin.Names.GetIconId(l.ItemId), _plugin.Names.GetName(l.ItemId));
                ItemContextMenu.AttachToLastItem(_plugin, l.ItemId);
                ImGui.TableNextColumn(); ImGui.Text(l.Quantity.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.Text(l.IsHq ? "★" : "");
                ImGui.TableNextColumn();
                if (l.UnitPriceGil > 0) ImGui.Text(l.UnitPriceGil.ToString("N0"));
                else ImGui.TextDisabled("(open sell list)");
                ImGui.TableNextColumn();
                if (mbMin > 0)
                {
                    ImGui.Text(mbMin.ToString("N0"));
                    if (!sourcedFromHome)
                    {
                        ImGui.SameLine();
                        ImGui.TextDisabled("*");
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("DC-wide fallback — no listings on your home world.");
                    }
                }
                else ImGui.TextDisabled("—");
                ImGui.TableNextColumn();
                if (l.UnitPriceGil > 0 && mbMin > 0)
                {
                    var delta = l.UnitPriceGil - mbMin;
                    var pct = mbMin > 0 ? (delta * 100.0 / mbMin) : 0;
                    var color = delta > 0 ? new Vector4(1f, 0.55f, 0.55f, 1f)     // above market — you'll be undercut
                              : delta < 0 ? new Vector4(0.55f, 1f, 0.55f, 1f)     // below market — likely to sell
                                          : new Vector4(0.9f, 0.9f, 0.9f, 1f);
                    var sign = delta > 0 ? "+" : "";
                    ImGui.TextColored(color, $"{sign}{delta:N0} ({sign}{pct:N1}%)");
                }
                else ImGui.TextDisabled("—");
            }
            ImGui.EndTable();
        }

        // Inventory (non-listing)
        ImGui.Text("Retainer inventory");
        if (ImGui.BeginTable("ktm_retainer_inv", 2, flags, new Vector2(0, 0)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableHeadersRow();

            foreach (var kv in r.Inventory.OrderByDescending(x => x.Value))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGuiIconExtensions.IconAndText(_plugin.TextureProviderRef, _plugin.Names.GetIconId(kv.Key), _plugin.Names.GetName(kv.Key));
                ItemContextMenu.AttachToLastItem(_plugin, kv.Key);
                ImGui.TableNextColumn(); ImGui.Text(kv.Value.ToString("N0"));
            }
            ImGui.EndTable();
        }
    }
}
