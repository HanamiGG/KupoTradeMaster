using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using KupoTradeMaster.Models;

namespace KupoTradeMaster.UI;

public sealed class JournalTab
{
    private readonly Plugin _plugin;
    private JournalEntry? _editing;
    private string _editPriceBuf = "";
    private string _lastExportMessage = "";
    // "" = all characters. Persists across tab switches but not across sessions.
    private string _characterFilter = "";

    public JournalTab(Plugin plugin) => _plugin = plugin;

    public void Draw()
    {
        var journal = _plugin.Journal;
        var sessionStart = _plugin.SessionStartUtc;

        var allEntries = journal.Snapshot();
        // Character filter: derive distinct owners present in the journal, plus current char if not there yet.
        var knownChars = allEntries.Select(e => e.CharacterKey).Where(k => !string.IsNullOrEmpty(k)).Distinct().OrderBy(k => k).ToArray();
        var currentChar = _plugin.CurrentCharacterKey();
        if (!string.IsNullOrEmpty(currentChar) && !knownChars.Contains(currentChar))
            knownChars = knownChars.Append(currentChar).OrderBy(k => k).ToArray();

        if (knownChars.Length > 0)
        {
            ImGui.Text("Character:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(220f);
            var label = string.IsNullOrEmpty(_characterFilter) ? "All characters" : _characterFilter;
            if (ImGui.BeginCombo("##charFilter", label))
            {
                if (ImGui.Selectable("All characters", string.IsNullOrEmpty(_characterFilter)))
                    _characterFilter = "";
                foreach (var ch in knownChars)
                {
                    if (ImGui.Selectable(ch, ch == _characterFilter))
                        _characterFilter = ch;
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            ImGui.TextDisabled($"({knownChars.Length} char{(knownChars.Length == 1 ? "" : "s")} in journal)");
        }

        // Filter early — everything below reads from `entries` and gets sliced automatically.
        var entries = string.IsNullOrEmpty(_characterFilter)
            ? allEntries
            : allEntries.Where(e => e.CharacterKey == _characterFilter).ToArray();
        var elapsed = DateTime.UtcNow - sessionStart;
        var gph = journal.SessionGilPerHour(sessionStart);
        var sessionEntries = entries.Where(e => e.TimestampUtc >= sessionStart).ToArray();
        var sessionSpent = sessionEntries.Where(e => e.Kind == JournalEntryKind.Buy).Sum(e => e.TotalGil);
        var sessionEarnedNet = sessionEntries.Where(e => e.Kind == JournalEntryKind.Sell).Sum(e => e.TotalGil - e.TaxGil);

        ImGui.Text($"Session {elapsed:hh\\:mm\\:ss}");
        ImGui.SameLine(); ImGui.TextDisabled("|");
        ImGui.SameLine(); ImGui.Text($"Buys: {sessionEntries.Count(e => e.Kind == JournalEntryKind.Buy)}");
        ImGui.SameLine(); ImGui.TextDisabled("|");
        ImGui.SameLine(); ImGui.Text($"Sells: {sessionEntries.Count(e => e.Kind == JournalEntryKind.Sell)}");
        ImGui.SameLine(); ImGui.TextDisabled("|");
        ImGui.SameLine(); ImGui.Text($"Spent: {sessionSpent:N0}");
        ImGui.SameLine(); ImGui.TextDisabled("|");
        ImGui.SameLine(); ImGui.Text($"Earned (net): {sessionEarnedNet:N0}");
        ImGui.SameLine(); ImGui.TextDisabled("|");
        ImGui.SameLine();
        var netColor = gph >= 0 ? new Vector4(0.55f, 1f, 0.55f, 1f) : new Vector4(1f, 0.55f, 0.55f, 1f);
        ImGui.TextColored(netColor, $"GP/hr: {gph:N0}");

        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 160f - ImGui.GetStyle().WindowPadding.X);
        if (ImGui.Button("Export CSV", new Vector2(160f, 0)))
        {
            try
            {
                var configDir = Plugin.PluginInterface.GetPluginConfigDirectory();
                var target = Path.Combine(configDir, $"journal_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                _plugin.Journal.ExportCsv(target, id => _plugin.Names.GetName(id));
                _lastExportMessage = $"Exported to {target}";
            }
            catch (Exception ex) { _lastExportMessage = $"Export failed: {ex.Message}"; }
        }

        ImGui.Separator();
        if (!string.IsNullOrEmpty(_lastExportMessage))
        {
            ImGui.TextDisabled(_lastExportMessage);
            ImGui.Spacing();
        }

        DrawDailyPLChart(entries);

        ImGui.TextDisabled("Right-click any row → set actual price on estimated sells, or delete corrections.");
        ImGui.Spacing();

        // Per-item aggregate — computed locally so the character filter applies.
        var agg = entries.GroupBy(e => e.ItemId).Select(g =>
        {
            var buys = g.Where(e => e.Kind == JournalEntryKind.Buy).ToArray();
            var sells = g.Where(e => e.Kind == JournalEntryKind.Sell).ToArray();
            var spent = buys.Sum(e => e.TotalGil);
            var earnedNet = sells.Sum(e => e.TotalGil - e.TaxGil);
            var a = new JournalAggregate(
                BuyQty: buys.Sum(e => e.Quantity),
                SellQty: sells.Sum(e => e.Quantity),
                SpentGil: spent,
                EarnedGil: earnedNet,
                NetGil: earnedNet - spent);
            return new KeyValuePair<uint, JournalAggregate>(g.Key, a);
        }).OrderByDescending(kv => Math.Abs(kv.Value.NetGil)).ToArray();

        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders |
                                      ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY;

        if (ImGui.BeginTable("ktm_journal_agg", 6, flags, new Vector2(0, ImGui.GetContentRegionAvail().Y * 0.55f)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableSetupColumn("Buys", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Sells", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Spent", ImGuiTableColumnFlags.WidthFixed, 110);
            ImGui.TableSetupColumn("Earned (net)", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Net P/L", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableHeadersRow();

            foreach (var kv in agg)
            {
                var name = _plugin.Names.GetName(kv.Key);
                var a = kv.Value;
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGuiIconExtensions.IconAndText(_plugin.TextureProviderRef, _plugin.Names.GetIconId(kv.Key), name);
                ItemContextMenu.AttachToLastItem(_plugin, kv.Key);
                ImGui.TableNextColumn(); ImGui.Text(a.BuyQty.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.Text(a.SellQty.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.Text(a.SpentGil.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.Text(a.EarnedGil.ToString("N0"));
                ImGui.TableNextColumn();
                var c = a.NetGil >= 0 ? new Vector4(0.55f, 1f, 0.55f, 1f) : new Vector4(1f, 0.55f, 0.55f, 1f);
                ImGui.TextColored(c, a.NetGil.ToString("N0"));
            }
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Recent activity");

        // Recent-activity log
        const ImGuiTableFlags recentFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("ktm_journal_recent", 6, recentFlags, new Vector2(0, 0)))
        {
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableSetupColumn("Kind", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Unit", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 110);
            ImGui.TableHeadersRow();

            foreach (var e in entries.OrderByDescending(x => x.TimestampUtc).Take(200))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text(e.TimestampUtc.ToLocalTime().ToString("MM-dd HH:mm:ss"));
                ImGui.TableNextColumn();
                var kindColor = e.Kind == JournalEntryKind.Buy
                    ? new Vector4(1f, 0.7f, 0.7f, 1f)
                    : new Vector4(0.7f, 1f, 0.7f, 1f);
                var kindLabel = (e.Kind == JournalEntryKind.Buy ? "BUY" : "SELL")
                              + (e.IsEstimated ? "?" : "");
                ImGui.TextColored(kindColor, kindLabel);
                if (e.IsEstimated && ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(e.Notes ?? "estimated entry");
                    ImGui.EndTooltip();
                }
                ImGui.TableNextColumn(); ImGuiIconExtensions.IconAndText(_plugin.TextureProviderRef, _plugin.Names.GetIconId(e.ItemId), _plugin.Names.GetName(e.ItemId) + (e.IsHq ? " *" : ""));
                DrawJournalRowContextMenu(e);
                ImGui.TableNextColumn(); ImGui.Text(e.Quantity.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.Text(e.UnitPriceGil.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.Text(e.TotalGil.ToString("N0"));
            }
            ImGui.EndTable();
        }

        if (entries.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("No entries yet. Purchase something from the market board to see your first buy log.");
        }

        DrawEditPopup();
    }

    private void DrawJournalRowContextMenu(JournalEntry e)
    {
        var popupId = $"ktm_journal_ctx_{e.TimestampUtc.Ticks}_{e.ItemId}";
        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup(popupId);

        if (!ImGui.BeginPopup(popupId)) return;
        try
        {
            ImGui.TextDisabled(_plugin.Names.GetName(e.ItemId));
            ImGui.Separator();

            if (e.IsEstimated && ImGui.MenuItem("Set actual unit price…"))
            {
                _editing = e;
                _editPriceBuf = e.UnitPriceGil.ToString();
                ImGui.CloseCurrentPopup();
            }
            if (e.IsEstimated && ImGui.MenuItem("Confirm entry as-is"))
            {
                _plugin.Journal.ReplaceEntry(e, e with { IsEstimated = false, Notes = (e.Notes ?? "") + " [user-confirmed]" });
            }
            if (ImGui.MenuItem("Delete entry"))
                _plugin.Journal.DeleteEntry(e);

            ImGui.Separator();
            if (ImGui.MenuItem("Copy item name")) ImGui.SetClipboardText(_plugin.Names.GetName(e.ItemId));
            if (ImGui.MenuItem("Open on Universalis"))
                ItemContextMenu.OpenUniversalis(e.ItemId, _plugin.Configuration.DataScope, _plugin.LogRef);
        }
        finally { ImGui.EndPopup(); }
    }

    private void DrawDailyPLChart(System.Collections.Generic.IReadOnlyList<JournalEntry> entries)
    {
        if (entries.Count == 0) return;

        // Bucket by local day; keep last 30
        var byDay = entries
            .GroupBy(e => e.TimestampUtc.ToLocalTime().Date)
            .Select(g => new
            {
                Day = g.Key,
                Buys = g.Where(e => e.Kind == JournalEntryKind.Buy).Sum(e => e.TotalGil),
                Sells = g.Where(e => e.Kind == JournalEntryKind.Sell).Sum(e => e.TotalGil - e.TaxGil),
            })
            .OrderBy(x => x.Day)
            .ToArray();
        if (byDay.Length < 1) return;

        var days = byDay.Skip(Math.Max(0, byDay.Length - 30)).ToArray();
        var maxAbs = Math.Max(1L, days.Max(d => Math.Max(d.Sells - d.Buys, 0)));
        var minAbs = Math.Min(0L, days.Min(d => d.Sells - d.Buys));
        var range = maxAbs - minAbs;

        var avail = ImGui.GetContentRegionAvail();
        var size = new Vector2(avail.X, 120f);
        var origin = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();

        var bg = ImGui.GetColorU32(new Vector4(0.10f, 0.10f, 0.12f, 0.85f));
        var axis = ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 0.4f));
        var green = ImGui.GetColorU32(new Vector4(0.35f, 1f, 0.55f, 0.9f));
        var red = ImGui.GetColorU32(new Vector4(1f, 0.5f, 0.5f, 0.9f));

        draw.AddRectFilled(origin, new Vector2(origin.X + size.X, origin.Y + size.Y), bg);
        // Zero line
        var zeroY = origin.Y + size.Y * (float)(maxAbs / (double)range);
        draw.AddLine(new Vector2(origin.X, zeroY), new Vector2(origin.X + size.X, zeroY), axis);

        var barW = size.X / days.Length * 0.7f;
        var slot = size.X / days.Length;
        for (var i = 0; i < days.Length; i++)
        {
            var net = days[i].Sells - days[i].Buys;
            var barTop = origin.Y + size.Y * (float)((maxAbs - Math.Max(net, 0)) / (double)range);
            var barBot = origin.Y + size.Y * (float)((maxAbs - Math.Min(net, 0)) / (double)range);
            var xCenter = origin.X + slot * (i + 0.5f);
            var col = net >= 0 ? green : red;
            draw.AddRectFilled(new Vector2(xCenter - barW / 2f, barTop),
                                new Vector2(xCenter + barW / 2f, barBot), col);
        }

        ImGui.Dummy(size);
        ImGui.TextDisabled($"Daily net (last {days.Length} days) — best {days.Max(d => d.Sells - d.Buys):N0} · worst {days.Min(d => d.Sells - d.Buys):N0}");
        ImGui.Spacing();
    }

    private void DrawEditPopup()
    {
        if (_editing == null) return;
        ImGui.OpenPopup("ktm_journal_edit");
        var open = true;
        if (ImGui.BeginPopupModal("ktm_journal_edit", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var e = _editing;
            ImGui.Text($"Set actual unit price for {_plugin.Names.GetName(e.ItemId)} × {e.Quantity}");
            ImGui.TextDisabled($"Recorded: {e.TimestampUtc.ToLocalTime()}");
            ImGui.Spacing();
            ImGui.SetNextItemWidth(160f);
            ImGui.InputText("gil per unit", ref _editPriceBuf, 16);
            ImGui.Spacing();
            if (ImGui.Button("Save"))
            {
                if (long.TryParse(_editPriceBuf, out var unit) && unit >= 0)
                {
                    var total = unit * e.Quantity;
                    var tax = (long)(total * _plugin.Configuration.TaxRate);
                    var updated = e with
                    {
                        UnitPriceGil = unit,
                        TotalGil = total,
                        TaxGil = tax,
                        IsEstimated = false,
                        Notes = (e.Notes ?? "") + " [user-corrected]"
                    };
                    _plugin.Journal.ReplaceEntry(e, updated);
                    _editing = null;
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) _editing = null;
            ImGui.EndPopup();
        }
        if (!open) _editing = null;
    }
}
