using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace KupoTradeMaster.UI;

public sealed class MainWindow : Window, IDisposable
{
    private readonly WatchlistTab _watchlist;
    private readonly BestDealsTab _bestDeals;
    private readonly ArbitrageTab _arbitrage;
    private readonly JournalTab _journal;
    private readonly WealthTab _wealth;
    private readonly RetainersTab _retainers;
    private readonly CraftingTab _crafting;
    private readonly SniperTab _sniper;
    private readonly SettingsTab _settings;

    public MainWindow(Plugin plugin)
        : base("KupoTradeMaster###KupoTradeMasterMain", ImGuiWindowFlags.None)
    {
        _watchlist = new WatchlistTab(plugin);
        _bestDeals = new BestDealsTab(plugin);
        _arbitrage = new ArbitrageTab(plugin);
        _journal   = new JournalTab(plugin);
        _wealth    = new WealthTab(plugin);
        _retainers = new RetainersTab(plugin);
        _crafting  = new CraftingTab(plugin);
        _sniper    = new SniperTab(plugin);
        _settings  = new SettingsTab(plugin);

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(760, 380),
            MaximumSize = new Vector2(2400, 2000),
        };
        Size = new Vector2(1050, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("ktm_tabs")) return;

        if (ImGui.BeginTabItem("Best Deals"))
        {
            _bestDeals.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Arbitrage"))
        {
            _arbitrage.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Watchlist"))
        {
            _watchlist.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Journal"))
        {
            _journal.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Wealth"))
        {
            _wealth.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Retainers"))
        {
            _retainers.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Crafting"))
        {
            _crafting.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Sniper"))
        {
            _sniper.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Settings"))
        {
            _settings.Draw();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    public void Dispose() => _crafting.Dispose();
}
