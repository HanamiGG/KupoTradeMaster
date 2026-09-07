using System;
using System.Diagnostics;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using KupoTradeMaster.Services;

namespace KupoTradeMaster.UI;

public static class ItemContextMenu
{
    /// Attach a right-click context menu to the last-emitted ImGui item (e.g. an item cell).
    /// Call immediately after drawing the row. All actions are read-only or affect local state.
    public static void AttachToLastItem(Plugin plugin, uint itemId)
    {
        var popupId = $"ktm_ctx_{itemId}";
        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup(popupId);

        if (!ImGui.BeginPopup(popupId)) return;
        try
        {
            var name = plugin.Names.GetName(itemId);
            ImGui.TextDisabled(name);
            ImGui.Separator();

            if (ImGui.MenuItem("Copy name"))
                ImGui.SetClipboardText(name);
            if (ImGui.MenuItem("Copy item ID"))
                ImGui.SetClipboardText(itemId.ToString());
            if (ImGui.MenuItem("Open on Universalis (browser)"))
                OpenUniversalis(itemId, plugin.Configuration.DataScope, plugin.LogRef);

            ImGui.Separator();
            var alreadyOnList = plugin.Configuration.Watchlist.Contains(itemId);
            if (alreadyOnList)
            {
                if (ImGui.MenuItem("Remove from Watchlist"))
                {
                    lock (plugin.Configuration.Sync)
                        plugin.Configuration.Watchlist.RemoveAll(id => id == itemId);
                    plugin.Configuration.Save();
                }
            }
            else if (ImGui.MenuItem("Add to Watchlist"))
            {
                lock (plugin.Configuration.Sync)
                    plugin.Configuration.Watchlist.Add(itemId);
                plugin.Configuration.Save();
            }

            // Per-item alert mute — shows current state if muted, one-click to extend.
            if (ImGui.BeginMenu("Mute alerts"))
            {
                var now = DateTime.UtcNow;
                var muted = plugin.Configuration.AlertMutedUntilUtc.TryGetValue(itemId, out var muteUntil)
                            && muteUntil > now;
                if (muted)
                {
                    ImGui.TextDisabled("Muted for " + FormatDuration(muteUntil - now));
                    if (ImGui.MenuItem("Unmute"))
                    {
                        lock (plugin.Configuration.Sync)
                            plugin.Configuration.AlertMutedUntilUtc.Remove(itemId);
                        plugin.Configuration.Save();
                    }
                    ImGui.Separator();
                }
                foreach (var (label, hours) in new[] { ("1 hour", 1), ("4 hours", 4), ("12 hours", 12), ("24 hours", 24), ("7 days", 168) })
                {
                    if (ImGui.MenuItem(label))
                    {
                        lock (plugin.Configuration.Sync)
                            plugin.Configuration.AlertMutedUntilUtc[itemId] = now.AddHours(hours);
                        plugin.Configuration.Save();
                    }
                }
                ImGui.EndMenu();
            }

            var groupNames = plugin.Groups.Names.ToArray();
            if (ImGui.BeginMenu("Groups"))
            {
                if (groupNames.Length == 0)
                {
                    ImGui.TextDisabled("(create groups in Settings)");
                }
                else
                {
                    foreach (var g in groupNames)
                    {
                        var inGroup = plugin.Groups.Members(g).Contains(itemId);
                        if (ImGui.MenuItem(g, "", inGroup))
                        {
                            if (inGroup) plugin.Groups.RemoveItem(g, itemId);
                            else         plugin.Groups.AddItem(g, itemId);
                        }
                    }
                }
                ImGui.EndMenu();
            }
        }
        finally { ImGui.EndPopup(); }
    }

    /// Human-friendly remaining time: "6d 3h", "12h 34m", or "45m". Uses Total* so multi-day
    /// spans don't collapse to a bogus small hour count (TimeSpan.Hours is 0-23, not total).
    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{Math.Max(0, (int)ts.TotalMinutes)}m";
    }

    public static void OpenUniversalis(uint itemId, string scope, IPluginLog log)
    {
        try
        {
            var url = $"https://universalis.app/market/{itemId}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            log.Info("KupoTradeMaster: opened {Url}", url);
        }
        catch (Exception ex) { log.Warning(ex, "KupoTradeMaster: failed to open Universalis URL"); }
    }
}
