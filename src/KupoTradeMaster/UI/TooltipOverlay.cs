using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using KupoTradeMaster.Services;

namespace KupoTradeMaster.UI;

public sealed class TooltipOverlay : Window, IDisposable
{
    private readonly Plugin _plugin;

    public TooltipOverlay(Plugin plugin)
        : base("KupoTradeMasterTooltip",
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav |
            ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar)
    {
        _plugin = plugin;
        RespectCloseHotkey = false;
        ForceMainWindow = false;
    }

    public override bool DrawConditions()
    {
        return _plugin.Configuration.TooltipEnabled && _plugin.Tooltip.CurrentHoveredItemId != 0;
    }

    public override void PreDraw()
    {
        var mouse = ImGui.GetMousePos();
        Position = new Vector2(mouse.X + 20, mouse.Y + 20);
        PositionCondition = ImGuiCond.Always;
    }

    public override void Draw()
    {
        var t = _plugin.Tooltip;
        var id = t.CurrentHoveredItemId;
        if (id == 0) return;
        var name = _plugin.Names.GetName(id);
        ImGui.TextUnformatted("KupoTradeMaster");
        ImGui.SameLine();
        ImGui.TextUnformatted(t.CurrentIsHq ? name + " (HQ)" : name);
        ImGui.Separator();
        var data = t.CurrentData;
        if (data == null)
        {
            ImGui.TextDisabled(t.IsFetching ? "Fetching..." : "No market data");
            return;
        }
        var home = _plugin.HomeWorldName;
        var listings = data.Listings;
        if (listings.Count > 0)
        {
            var hwMin = PriceLens.HomeWorldMin(data, home);
            if (hwMin > 0)
            {
                ImGui.TextUnformatted("Min (" + home + "): " + hwMin.ToString("N0") + " gil");
                var nqHw = listings.Where(l => !l.Hq && l.WorldName == home).ToList();
                var hqHw = listings.Where(l => l.Hq  && l.WorldName == home).ToList();
                if (nqHw.Count > 0) ImGui.TextUnformatted("  NQ: " + nqHw.Min(l => l.PricePerUnit).ToString("N0"));
                if (hqHw.Count > 0) ImGui.TextUnformatted("  HQ: " + hqHw.Min(l => l.PricePerUnit).ToString("N0"));
            }
            else
            {
                var dcMin = listings.Min(l => l.PricePerUnit);
                ImGui.TextUnformatted("Min (DC): " + dcMin.ToString("N0") + " gil");
                ImGui.TextDisabled("  no listings on " + (home ?? "home world"));
            }
        }
        else if (data.MinPrice > 0)
        {
            ImGui.TextUnformatted("Min (agg): " + data.MinPrice.ToString("N0"));
        }
        else
        {
            ImGui.TextDisabled("No active listings");
        }
        if (data.CurrentAveragePrice > 0) ImGui.TextUnformatted("Avg (DC): " + data.CurrentAveragePrice.ToString("N0") + " gil");
        if (data.RegularSaleVelocity > 0) ImGui.TextUnformatted("Vel:  " + data.RegularSaleVelocity.ToString("0.0") + "/day");
        var vendor = _plugin.Names.GetVendorPrice(id);
        if (vendor > 0) ImGui.TextUnformatted("Vendor: " + vendor.ToString("N0") + " gil");
        ImGui.Separator();
        ImGui.TextDisabled("scope: " + _plugin.Configuration.DataScope);
    }

    public void Dispose() { }
}
