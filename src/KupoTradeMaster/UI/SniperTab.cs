using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using KupoTradeMaster.Services;

namespace KupoTradeMaster.UI;

/// Single-item marketboard scan. User picks an item, we one-shot fetch full listings for that item
/// across the configured scope, then flag rows priced below the current buy formula. Read-only —
/// no auto-buy, no addon injection. User still has to open the MB in-game and click.
public sealed class SniperTab
{
    private readonly Plugin _plugin;
    private string _searchBuf = "";
    private uint _selectedItem;
    private string _selectedName = "";
    private UniversalisItem? _snapshot;
    private DateTime _snapshotAtUtc;
    private bool _fetching;
    private string _status = "";
    private string _formulaBuf = "";
    private string _formulaStatus = "";
    private PriceDsl.INode? _formulaNode;

    public SniperTab(Plugin plugin)
    {
        _plugin = plugin;
        _formulaBuf = plugin.Configuration.BuyFormula;
    }

    public void Draw()
    {
        ImGui.TextDisabled("Sniper — read-only single-item scan. No auto-buy.");
        ImGui.Spacing();

        // Item picker
        ImGui.SetNextItemWidth(320f);
        ImGui.InputTextWithHint("##sniperSearch", "Type an item name…", ref _searchBuf, 64);
        if (!string.IsNullOrWhiteSpace(_searchBuf))
        {
            var results = _plugin.Names.Search(_searchBuf, 8).ToArray();
            foreach (var r in results)
            {
                if (ImGui.Selectable($"{r.Name} (#{r.Id})", r.Id == _selectedItem))
                {
                    _selectedItem = r.Id;
                    _selectedName = r.Name;
                    _snapshot = null;
                    _status = "";
                    _searchBuf = "";
                }
            }
        }

        if (_selectedItem == 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Select an item to scan.");
            return;
        }

        ImGui.Spacing();
        ImGuiIconExtensions.IconAndText(_plugin.TextureProviderRef, _plugin.Names.GetIconId(_selectedItem), _selectedName);
        ImGui.SameLine();
        if (ImGui.Button(_fetching ? "Scanning…" : "Scan now"))
            _ = Fetch();
        if (_snapshot != null)
        {
            var age = DateTime.UtcNow - _snapshotAtUtc;
            ImGui.SameLine();
            ImGui.TextDisabled($"({age.TotalSeconds:0}s old)");
        }
        if (!string.IsNullOrEmpty(_status))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(_status);
        }

        ImGui.Spacing();
        ImGui.Text("Buy formula (rows at or below this highlight green):");
        ImGui.SetNextItemWidth(400f);
        if (ImGui.InputText("##sniperFormula", ref _formulaBuf, 256))
        {
            if (string.IsNullOrWhiteSpace(_formulaBuf))
            {
                _formulaNode = null;
                _formulaStatus = "";
            }
            else if (PriceDsl.TryCompile(_formulaBuf, _plugin.CompiledCustomSources(), out var node, out var err) && node != null)
            {
                _formulaNode = node;
                _formulaStatus = "ok";
            }
            else
            {
                _formulaNode = null;
                _formulaStatus = "err: " + err;
            }
        }
        ImGui.SameLine();
        if (!string.IsNullOrEmpty(_formulaStatus))
            ImGui.TextDisabled(_formulaStatus);

        ImGui.Separator();

        if (_snapshot == null)
        {
            ImGui.TextDisabled(_fetching ? "Fetching Universalis…" : "Click 'Scan now' to fetch listings.");
            return;
        }

        var listings = _snapshot.Listings.OrderBy(l => l.PricePerUnit).ToArray();
        if (listings.Length == 0)
        {
            ImGui.TextDisabled("No active listings on the market board.");
            return;
        }

        double? threshold = null;
        if (_formulaNode != null)
        {
            try
            {
                var ctx = new PriceDslContext(
                    ItemId: _selectedItem,
                    MinPrice: listings.Min(l => (double)l.PricePerUnit),
                    AvgPrice: _snapshot.CurrentAveragePrice,
                    Velocity: _snapshot.RegularSaleVelocity,
                    Vendor: _plugin.Names.GetVendorPrice(_selectedItem),
                    TaxRate: _plugin.Configuration.TaxRate);
                threshold = _formulaNode.Eval(ctx);
            }
            catch { threshold = null; }
        }

        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders |
                                      ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY;
        if (!ImGui.BeginTable("ktm_sniper", 5, flags, new Vector2(0, 0))) return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Unit", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 60);
        ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("Retainer @ World", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        foreach (var l in listings)
        {
            var flagged = threshold.HasValue && l.PricePerUnit <= threshold.Value;
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (flagged)
                ImGui.TextColored(new Vector4(0.55f, 1f, 0.55f, 1f), l.PricePerUnit.ToString("N0"));
            else
                ImGui.Text(l.PricePerUnit.ToString("N0"));
            ImGui.TableNextColumn(); ImGui.Text(l.Quantity.ToString("N0"));
            ImGui.TableNextColumn(); ImGui.Text((l.PricePerUnit * (long)l.Quantity).ToString("N0"));
            ImGui.TableNextColumn(); ImGui.Text(l.Hq ? "★" : "");
            ImGui.TableNextColumn(); ImGui.Text($"{l.RetainerName ?? "?"} @ {l.WorldName ?? "?"}");
        }
        ImGui.EndTable();
    }

    private async Task Fetch()
    {
        if (_fetching || _selectedItem == 0) return;
        _fetching = true;
        _status = "";
        try
        {
            var result = await _plugin.Universalis.FetchAsync(
                _plugin.Configuration.DataScope,
                new[] { _selectedItem },
                CancellationToken.None).ConfigureAwait(false);
            _snapshot = result.TryGetValue(_selectedItem, out var it) ? it : null;
            _snapshotAtUtc = DateTime.UtcNow;
            if (_snapshot == null) _status = "no data returned";
        }
        catch (Exception ex) { _status = "err: " + ex.Message; }
        finally { _fetching = false; }
    }
}
