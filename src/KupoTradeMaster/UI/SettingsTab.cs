using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using KupoTradeMaster.Services;

namespace KupoTradeMaster.UI;

public sealed class SettingsTab
{
    private string _newGroupName = "";
    private string _renameBuffer = "";
    private string? _renamingGroup = null;
    private string _formulaBuffer = "";
    private bool _formulaBufferSeeded;
    private string _formulaStatus = "";
    private string _newSourceName = "";
    private string _newSourceFormula = "";
    private string _sourceStatus = "";
    private string _newOpName = "";
    private string _opStatus = "";
    private string _backupImportPath = "";
    private string _backupStatus = "";
    private string _shareStatus = "";
    private string _importBuffer = "";
    private static readonly string[] KnownDataCenters =
    {
        "Aether", "Primal", "Crystal", "Dynamis",             // NA
        "Chaos", "Light", "Materia",                            // EU + OCE
        "Elemental", "Gaia", "Mana", "Meteor",                 // JP
        "Shadow",                                               // Meteor grouping variants
    };

    private readonly Plugin _plugin;

    public SettingsTab(Plugin plugin) => _plugin = plugin;

    public void Draw()
    {
        var cfg = _plugin.Configuration;
        var dirty = false;

        ImGui.TextDisabled("KupoTradeMaster settings — changes save immediately.");
        ImGui.Spacing();

        // Data scope (DC / World)
        ImGui.Text("Universalis data scope");
        ImGui.SameLine();
        HelpMarker("The world, data center, or region KupoTradeMaster queries for prices. Home DC is usually the right choice for retainer flipping; use World for single-world stats.");

        ImGui.SetNextItemWidth(220f);
        var scope = cfg.DataScope;
        if (ImGui.BeginCombo("##scope", scope))
        {
            foreach (var dc in KnownDataCenters)
            {
                var selected = dc == scope;
                if (ImGui.Selectable(dc, selected))
                {
                    cfg.DataScope = dc;
                    dirty = true;
                }
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220f);
        var custom = cfg.DataScope;
        if (ImGui.InputTextWithHint("##scopeCustom", "or type a world/DC/region", ref custom, 32) && custom != cfg.DataScope)
        {
            cfg.DataScope = custom;
            dirty = true;
        }

        ImGui.Spacing();

        // Tax rate
        ImGui.Text("MB tax rate");
        ImGui.SameLine();
        HelpMarker("Auto-updated every 15 min to the minimum current city rate for your home world. Override here if you want to plan against a specific city's tax instead of the cheapest.");
        var taxPct = (float)(cfg.TaxRate * 100.0);
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderFloat("##tax", ref taxPct, 3.0f, 5.0f, "%.1f%%"))
        {
            cfg.TaxRate = taxPct / 100.0;
            dirty = true;
        }
        if (_plugin.TaxRates.MinRatePercent.HasValue)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"live min: {_plugin.TaxRates.MinRatePercent}% ({_plugin.TaxRates.MinCityName})");
        }

        ImGui.Spacing();

        // Polling
        ImGui.Text("Per-item minimum poll interval");
        ImGui.SameLine();
        HelpMarker("Minimum seconds between refetching the same watchlist item from Universalis. Larger = fewer API calls; smaller = fresher data. Universalis' hard cap is 25 req/s across all users on your IP.");
        var poll = cfg.PerItemMinPollSeconds;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderInt("##poll", ref poll, 30, 300, "%d s"))
        {
            cfg.PerItemMinPollSeconds = poll;
            dirty = true;
        }

        ImGui.Separator();
        ImGui.Text("Detected prerequisite plugins");
        var b = _plugin.Bridge;
        if (ImGui.Button("Rescan installed plugins")) b.Rescan();
        BulletBadge("Allagan Tools", b.HasAllaganTools,
            b.HasAllaganTools ? "seeds retainer roster from AT" : "install to auto-discover retainers without visiting bells");
        BulletBadge("AutoRetainer", b.HasAutoRetainer,
            b.HasAutoRetainer ? "offline retainer gil + venture status" : "install for offline retainer gil + venture ETA");
        BulletBadge("Artisan", b.HasArtisan,
            b.HasArtisan ? "detected (used by future Crafting tab)" : "not currently needed");

        ImGui.Separator();
        ImGui.Text("Buy-under formula (TSM-style)");
        ImGui.SameLine();
        HelpMarker("Optional. If set, items whose current min listing is at or below the formula value get flagged as buys on the Best Deals / Watchlist views. Sources: mbmin, mbavg (aka dbmarket), velocity, vendor, taxrate. Functions: max, min, avg, round, floor, ceil, if. Example: max(dbmarket * 0.7, vendor * 1.5)");

        // Seed the input from persisted config exactly ONCE per session — otherwise selecting-all
        // and deleting to type a new formula silently refills the field with the old text next frame.
        if (!_formulaBufferSeeded)
        {
            _formulaBuffer = cfg.BuyFormula ?? "";
            _formulaBufferSeeded = true;
        }
        ImGui.SetNextItemWidth(420f);
        var formulaChanged = ImGui.InputTextWithHint("##ktm_formula", "e.g. dbmarket * 0.7", ref _formulaBuffer, 200);
        ImGui.SameLine();
        if (ImGui.Button("Apply"))
        {
            if (string.IsNullOrWhiteSpace(_formulaBuffer))
            {
                cfg.BuyFormula = "";
                cfg.Save();
                _formulaStatus = "cleared";
                dirty = false;
            }
            else if (PriceDsl.TryCompile(_formulaBuffer, out _, out var err))
            {
                cfg.BuyFormula = _formulaBuffer;
                cfg.Save();
                _formulaStatus = "valid — applied";
                dirty = false;
            }
            else
            {
                _formulaStatus = $"parse error: {err}";
            }
        }
        if (!string.IsNullOrEmpty(_formulaStatus)) ImGui.TextDisabled(_formulaStatus);

        ImGui.Separator();
        ImGui.Text("Watchlist groups");
        ImGui.SameLine();
        HelpMarker("Groups are an organizational overlay over the flat Watchlist. Filter the Watchlist / Best Deals views by group. Add items to a group via the right-click menu on any item row.");

        ImGui.SetNextItemWidth(240f);
        ImGui.InputTextWithHint("##ktm_new_group", "e.g. Consumables/Food/HQ Only", ref _newGroupName, 64);
        ImGui.SameLine();
        if (ImGui.Button("Create") && _plugin.Groups.Create(_newGroupName))
            _newGroupName = "";
        ImGui.SameLine();
        HelpMarker("Use '/' to nest groups (TSM-style). e.g. Consumables/Food/HQ Only. Nesting is presentation-only — a nested group is still an independent list of items.");

        if (ImGui.BeginTable("ktm_groups_tbl", 5,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders,
                new Vector2(0, 260f)))
        {
            ImGui.TableSetupColumn("Group", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Items", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Operation", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("Formula override (blank = use op/global)", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn(" ", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableHeadersRow();

            foreach (var g in _plugin.Groups.Names.ToArray())
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (_renamingGroup == g)
                {
                    ImGui.SetNextItemWidth(130f);
                    ImGui.InputText($"##ktm_rn_{g}", ref _renameBuffer, 32);
                }
                else
                {
                    // Render nesting via indentation on '/' depth; leaf name only, tooltip has full path.
                    var depth = g.Count(c => c == '/');
                    var leaf = g.Substring(g.LastIndexOf('/') + 1);
                    if (depth > 0) ImGui.Indent(depth * 12f);
                    ImGui.Text(leaf);
                    if (depth > 0)
                    {
                        ImGui.Unindent(depth * 12f);
                        if (ImGui.IsItemHovered()) { ImGui.BeginTooltip(); ImGui.Text(g); ImGui.EndTooltip(); }
                    }
                }

                ImGui.TableNextColumn();
                ImGui.Text(_plugin.Groups.Members(g).Count.ToString());

                ImGui.TableNextColumn();
                var attached = cfg.GroupOperations.TryGetValue(g, out var opN) ? opN : "";
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo($"##opFor{g}", string.IsNullOrEmpty(attached) ? "(none)" : attached))
                {
                    if (ImGui.Selectable("(none)", string.IsNullOrEmpty(attached)))
                    {
                        cfg.GroupOperations.Remove(g);
                        cfg.Save();
                    }
                    foreach (var opName in cfg.Operations.Keys.OrderBy(k => k))
                    {
                        if (ImGui.Selectable(opName, opName == attached))
                        {
                            cfg.GroupOperations[g] = opName;
                            cfg.Save();
                        }
                    }
                    ImGui.EndCombo();
                }

                ImGui.TableNextColumn();
                var current = cfg.GroupFormulas.TryGetValue(g, out var f) ? f : "";
                var buf = current;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText($"##ktm_gf_{g}", ref buf, 200))
                {
                    if (string.IsNullOrWhiteSpace(buf))
                    {
                        _plugin.Groups.SetFormula(g, "");
                    }
                    else if (PriceDsl.TryCompile(buf, out _, out _))
                    {
                        _plugin.Groups.SetFormula(g, buf);
                    }
                    // Silently ignore parse errors until user finishes typing; global formula stays valid.
                }

                ImGui.TableNextColumn();
                if (_renamingGroup == g)
                {
                    if (ImGui.SmallButton($"Save##save{g}") && _plugin.Groups.Rename(g, _renameBuffer))
                    {
                        _renamingGroup = null; _renameBuffer = "";
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Cancel##cancel{g}")) { _renamingGroup = null; _renameBuffer = ""; }
                }
                else
                {
                    if (ImGui.SmallButton($"Rename##rn{g}")) { _renamingGroup = g; _renameBuffer = g; }
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Delete##del{g}")) _plugin.Groups.Delete(g);
                }
            }
            ImGui.EndTable();
        }

        ImGui.Separator();
        ImGui.Text("Custom price sources");
        ImGui.SameLine();
        HelpMarker("Named formula fragments reusable across group/global formulas. Later sources can reference earlier ones by name.");

        ImGui.SetNextItemWidth(140f);
        ImGui.InputTextWithHint("##ktm_src_name", "source name", ref _newSourceName, 32);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(280f);
        ImGui.InputTextWithHint("##ktm_src_formula", "formula (e.g. dbmarket * 0.7)", ref _newSourceFormula, 200);
        ImGui.SameLine();
        if (ImGui.Button("Add source"))
        {
            var n = (_newSourceName ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(n)) _sourceStatus = "name required";
            else if (PriceDsl.TryCompile(_newSourceFormula, out _, out var err))
            {
                cfg.CustomSources[n] = _newSourceFormula;
                cfg.Save();
                _newSourceName = ""; _newSourceFormula = "";
                _sourceStatus = "added";
            }
            else _sourceStatus = $"parse error: {err}";
        }
        if (!string.IsNullOrEmpty(_sourceStatus)) ImGui.TextDisabled(_sourceStatus);

        if (cfg.CustomSources.Count > 0 && ImGui.BeginTable("ktm_sources_tbl", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders,
                new Vector2(0, 140f)))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("Formula", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn(" ", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableHeadersRow();
            foreach (var (name, formula) in cfg.CustomSources.ToArray())
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text(name);
                ImGui.TableNextColumn();
                var buf = formula;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText($"##ktm_src_{name}", ref buf, 200))
                {
                    if (PriceDsl.TryCompile(buf, out _, out _))
                    {
                        cfg.CustomSources[name] = buf;
                        cfg.Save();
                    }
                }
                ImGui.TableNextColumn();
                if (ImGui.SmallButton($"Delete##delsrc{name}"))
                {
                    cfg.CustomSources.Remove(name);
                    cfg.Save();
                }
            }
            ImGui.EndTable();
        }

        ImGui.Separator();
        ImGui.Text("Operations (TSM-style presets)");
        ImGui.SameLine();
        HelpMarker("An operation is a named preset of buy/sell/restock parameters. Attach one to a group in the Groups table; it overrides the per-group formula. Reuse the same operation across many groups without duplicating.");

        ImGui.SetNextItemWidth(220f);
        ImGui.InputTextWithHint("##opNewName", "operation name", ref _newOpName, 40);
        ImGui.SameLine();
        if (ImGui.Button("Create operation"))
        {
            var n = (_newOpName ?? "").Trim();
            if (string.IsNullOrEmpty(n)) _opStatus = "name required";
            else if (cfg.Operations.ContainsKey(n)) _opStatus = "already exists";
            else
            {
                cfg.Operations[n] = new Operation();
                cfg.Save();
                _newOpName = "";
                _opStatus = "";
            }
        }
        if (!string.IsNullOrEmpty(_opStatus)) { ImGui.SameLine(); ImGui.TextDisabled(_opStatus); }

        if (cfg.Operations.Count > 0 && ImGui.BeginTable("ktm_ops_tbl", 5,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders,
                new Vector2(0, 220f)))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 160);
            ImGui.TableSetupColumn("Buy formula", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Restock", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Min", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn(" ", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableHeadersRow();

            foreach (var opName in cfg.Operations.Keys.OrderBy(k => k).ToArray())
            {
                var op = cfg.Operations[opName];
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text(opName);
                ImGui.TableNextColumn();
                var buyBuf = op.BuyFormula;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText($"##opBuy{opName}", ref buyBuf, 200))
                {
                    op.BuyFormula = buyBuf;
                    cfg.Save();
                }
                ImGui.TableNextColumn();
                var restock = op.RestockQuantity;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt($"##opRestock{opName}", ref restock, 0))
                {
                    op.RestockQuantity = Math.Max(0, restock);
                    cfg.Save();
                }
                ImGui.TableNextColumn();
                var minRestock = op.MinRestockQuantity;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt($"##opMin{opName}", ref minRestock, 0))
                {
                    op.MinRestockQuantity = Math.Max(0, minRestock);
                    cfg.Save();
                }
                ImGui.TableNextColumn();
                if (ImGui.SmallButton($"Delete##delop{opName}"))
                {
                    cfg.Operations.Remove(opName);
                    // Detach from any groups that referenced it.
                    var refs = cfg.GroupOperations.Where(kv => kv.Value == opName).Select(kv => kv.Key).ToList();
                    foreach (var g in refs) cfg.GroupOperations.Remove(g);
                    cfg.Save();
                }
            }
            ImGui.EndTable();
        }

        ImGui.Separator();
        ImGui.Text("Import / export");
        ImGui.SameLine();
        HelpMarker("Share your groups + formulas + custom sources as a copy-pasteable KM1: code. Paste someone else's code and click Import to merge.");

        if (ImGui.Button("Copy full bundle to clipboard"))
        {
            var code = ShareCodec.EncodeBundle(cfg.Groups, cfg.GroupFormulas, cfg.CustomSources, cfg.BuyFormula);
            ImGui.SetClipboardText(code);
            _shareStatus = $"copied ({code.Length} chars)";
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(280f);
        ImGui.InputTextWithHint("##ktm_import", "paste KM1:... code", ref _importBuffer, 4096);
        ImGui.SameLine();
        if (ImGui.Button("Import"))
        {
            if (ShareCodec.TryDecode(_importBuffer, out var p, out var err) && p != null)
            {
                MergeShare(p, cfg);
                cfg.Save();
                _shareStatus = $"imported '{p.Kind}'";
                _importBuffer = "";
            }
            else _shareStatus = $"import failed: {err}";
        }
        if (!string.IsNullOrEmpty(_shareStatus)) ImGui.TextDisabled(_shareStatus);

        ImGui.Separator();
        ImGui.Text("Notifications");
        var notify = cfg.BuySignalChatNotifications;
        if (ImGui.Checkbox("Ping chat when a formula-flagged buy appears", ref notify))
        {
            cfg.BuySignalChatNotifications = notify;
            dirty = true;
        }
        ImGui.SameLine();
        HelpMarker("When an item on your Watchlist first drops at or below its buy formula threshold this session, KupoTradeMaster writes a one-time chat line so you can act. Re-triggers if the item goes above and back below.");

        var tooltip = cfg.TooltipEnabled;
        if (ImGui.Checkbox("Show market data overlay when hovering items in-game", ref tooltip))
        {
            cfg.TooltipEnabled = tooltip;
            dirty = true;
        }
        ImGui.SameLine();
        HelpMarker("A small ImGui popup renders next to the cursor with min/avg/velocity/vendor for the hovered item. Coexists with PriceInsight and other tooltip plugins — doesn't touch the game's ItemDetail node.");

        ImGui.Separator();
        ImGui.Text("Data files");
        var configDir = Plugin.PluginInterface.GetPluginConfigDirectory();
        ImGui.TextDisabled(configDir);
        if (ImGui.Button("Open config folder"))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = configDir, UseShellExecute = true }); }
            catch { /* graceful */ }
        }

        ImGui.SameLine();
        if (ImGui.Button("Backup → zip"))
        {
            try
            {
                var target = System.IO.Path.Combine(configDir, $"ktm_backup_{System.DateTime.Now:yyyyMMdd_HHmmss}.zip");
                _plugin.Backup.Export(target);
                _backupStatus = $"exported → {System.IO.Path.GetFileName(target)}";
            }
            catch (System.Exception ex) { _backupStatus = "export failed: " + ex.Message; }
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220f);
        ImGui.InputTextWithHint("##backupPath", "path to .zip to restore", ref _backupImportPath, 260);
        ImGui.SameLine();
        if (ImGui.Button("Restore"))
        {
            try
            {
                _plugin.Backup.Import(_backupImportPath);
                _backupStatus = "restored (reload Dalamud plugin to reload config.json)";
            }
            catch (System.Exception ex) { _backupStatus = "restore failed: " + ex.Message; }
        }
        if (!string.IsNullOrEmpty(_backupStatus)) ImGui.TextDisabled(_backupStatus);

        if (dirty) cfg.Save();
    }

    private static void MergeShare(SharePayload p, Configuration cfg)
    {
        lock (cfg.Sync)
        {
            switch (p.Kind)
            {
                case "group":
                    if (!string.IsNullOrWhiteSpace(p.Name) && p.ItemIds != null)
                    {
                        cfg.Groups[p.Name] = new System.Collections.Generic.List<uint>(p.ItemIds);
                        foreach (var id in p.ItemIds)
                            if (!cfg.Watchlist.Contains(id)) cfg.Watchlist.Add(id);
                        if (!string.IsNullOrWhiteSpace(p.Formula))
                            cfg.GroupFormulas[p.Name] = p.Formula;
                    }
                    break;
                case "customSources":
                    if (p.CustomSources != null)
                        foreach (var (k, v) in p.CustomSources) cfg.CustomSources[k.ToLowerInvariant()] = v;
                    break;
                case "bundle":
                    if (p.Groups != null)
                        foreach (var (k, v) in p.Groups) cfg.Groups[k] = new System.Collections.Generic.List<uint>(v);
                    if (p.GroupFormulas != null)
                        foreach (var (k, v) in p.GroupFormulas) cfg.GroupFormulas[k] = v;
                    if (p.CustomSources != null)
                        foreach (var (k, v) in p.CustomSources) cfg.CustomSources[k.ToLowerInvariant()] = v;
                    if (!string.IsNullOrWhiteSpace(p.BuyFormula)) cfg.BuyFormula = p.BuyFormula!;
                    if (p.Groups != null)
                        foreach (var kv in p.Groups)
                            foreach (var id in kv.Value)
                                if (!cfg.Watchlist.Contains(id)) cfg.Watchlist.Add(id);
                    break;
            }
        }
    }

    private static void HelpMarker(string text)
    {
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 30f);
            ImGui.Text(text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    private static void BulletBadge(string label, bool on, string tooltip)
    {
        var color = on ? new Vector4(0.35f, 1f, 0.55f, 1f) : new Vector4(0.55f, 0.55f, 0.55f, 1f);
        ImGui.TextColored(color, on ? $"  ● {label}" : $"  ○ {label}");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(tooltip);
            ImGui.EndTooltip();
        }
    }
}
