using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using KupoTradeMaster.Models;

namespace KupoTradeMaster.UI;

public sealed class WealthTab
{
    private readonly Plugin _plugin;
    // Guards the "Snapshot now" button against double-clicks (each capture takes seconds and hits
    // Universalis + writes history.jsonl; concurrent captures both queue up on the file lock).
    private volatile bool _snapshotting;

    public WealthTab(Plugin plugin) => _plugin = plugin;

    public void Draw()
    {
        var val = _plugin.Valuation;
        var snap = val.Latest;

        if (snap == null)
        {
            ImGui.TextDisabled("Waiting for first inventory read (up to 25 seconds after login)…");
            return;
        }

        ImGui.Text($"Snapshot: {snap.CapturedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        ImGui.SameLine();
        if (_snapshotting) ImGui.BeginDisabled();
        if (ImGui.Button("Snapshot now") && !_snapshotting)
        {
            _snapshotting = true;
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try { await val.CaptureAsync(default).ConfigureAwait(false); }
                finally { _snapshotting = false; }
            });
        }
        if (_snapshotting) ImGui.EndDisabled();

        ImGui.Spacing();

        // Big total
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.95f, 0.5f, 1f));
        ImGui.SetWindowFontScale(1.6f);
        ImGui.Text($"{snap.TotalGil:N0} gil");
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopStyleColor();

        ImGui.TextDisabled($"across {snap.UniqueItemsValued} valued items");
        ImGui.Separator();

        if (ImGui.BeginTable("wealth_breakdown", 2,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Bucket", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Gil", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableHeadersRow();

            Row("Cash on character", snap.GilOnCharacter);
            Row("Bag + crystals (min-listing value)", snap.BagValueGil);
            Row("Saddlebag (min-listing value)", snap.SaddlebagValueGil);
            Row("Retainer gil", snap.GilInRetainers);
            Row("Retainer inventory (min-listing value)", snap.RetainerInventoryValueGil);
            Row("Active listings (× MB min)", snap.ActiveListingsValueGil);
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Retainer numbers populate as you visit each retainer via a Summoning Bell.");
        ImGui.Separator();

        // Multi-series net-worth chart over last N snapshots
        var history = val.HistorySnapshot();
        if (history.Count >= 2)
        {
            var window = Math.Min(96, history.Count);
            var slice = history.Skip(history.Count - window).ToArray();
            ImGui.Text($"Net-worth trend — last {window} snapshots ({window * 5} min at 5-min cadence)");
            DrawMultiSeriesChart(slice);
        }
        else
        {
            ImGui.TextDisabled("Need at least 2 snapshots to draw a trend line (snapshots happen every 5 min).");
        }
    }

    private static void Row(string label, long gil)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.Text(label);
        ImGui.TableNextColumn(); ImGui.Text(gil.ToString("N0"));
    }

    private static readonly (string Label, Vector4 Color, Func<NetWorthSnapshot, long> Get)[] Series =
    {
        ("Total",     new Vector4(1f,    0.95f, 0.4f,  1f), s => s.TotalGil),
        ("Cash",      new Vector4(0.4f,  0.75f, 1f,    1f), s => s.GilOnCharacter),
        ("Bag+SB",    new Vector4(0.55f, 1f,    0.55f, 1f), s => s.BagValueGil + s.SaddlebagValueGil),
        ("Retainers", new Vector4(0.9f,  0.6f,  1f,    1f), s => s.GilInRetainers + s.RetainerInventoryValueGil + s.ActiveListingsValueGil),
    };

    private static void DrawMultiSeriesChart(NetWorthSnapshot[] samples)
    {
        if (samples.Length < 2) return;

        // Shared Y-axis scaled to overall min/max across all series so lines are comparable.
        long yMin = long.MaxValue, yMax = long.MinValue;
        foreach (var s in samples)
        {
            foreach (var series in Series)
            {
                var v = series.Get(s);
                if (v < yMin) yMin = v;
                if (v > yMax) yMax = v;
            }
        }
        if (yMax <= yMin) yMax = yMin + 1;

        var avail = ImGui.GetContentRegionAvail();
        var size = new Vector2(avail.X, MathF.Max(160f, MathF.Min(320f, avail.Y - 60f)));
        var origin = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();

        var bg = ImGui.GetColorU32(new Vector4(0.10f, 0.10f, 0.12f, 0.85f));
        var axis = ImGui.GetColorU32(new Vector4(0.45f, 0.45f, 0.45f, 0.35f));

        draw.AddRectFilled(origin, new Vector2(origin.X + size.X, origin.Y + size.Y), bg);

        // Horizontal grid lines at 0%, 25%, 50%, 75%, 100%
        for (var i = 0; i <= 4; i++)
        {
            var y = origin.Y + size.Y * (1f - i / 4f);
            draw.AddLine(new Vector2(origin.X, y), new Vector2(origin.X + size.X, y), axis);
        }

        // Series polylines
        foreach (var series in Series)
        {
            var col = ImGui.GetColorU32(series.Color);
            for (var i = 1; i < samples.Length; i++)
            {
                var x0 = origin.X + size.X * (i - 1f) / (samples.Length - 1);
                var x1 = origin.X + size.X * i / (samples.Length - 1);
                var v0 = series.Get(samples[i - 1]);
                var v1 = series.Get(samples[i]);
                var y0 = origin.Y + size.Y * (1f - (v0 - yMin) / (float)(yMax - yMin));
                var y1 = origin.Y + size.Y * (1f - (v1 - yMin) / (float)(yMax - yMin));
                var thickness = series.Label == "Total" ? 2.2f : 1.3f;
                draw.AddLine(new Vector2(x0, y0), new Vector2(x1, y1), col, thickness);
            }
        }

        ImGui.Dummy(size);

        // Y-axis label strip below chart
        ImGui.TextDisabled($"min {yMin:N0}   max {yMax:N0}   Δ {Series[0].Get(samples[^1]) - Series[0].Get(samples[0]):N0}");

        // Legend
        foreach (var s in Series)
        {
            ImGui.SameLine(); ImGui.TextColored(s.Color, "  ● ");
            ImGui.SameLine(); ImGui.TextDisabled(s.Label);
        }
    }
}
