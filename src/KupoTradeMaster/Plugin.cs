using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Command;
using Dalamud.Game.Network.Structures;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KupoTradeMaster.Models;
using KupoTradeMaster.Services;
using KupoTradeMaster.UI;

namespace KupoTradeMaster;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/kupo";
    private const string CommandAlt  = "/ktm";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager        CommandManager  { get; private set; } = null!;
    [PluginService] internal static IPluginLog             Log             { get; private set; } = null!;
    [PluginService] internal static IDataManager           DataManager     { get; private set; } = null!;
    [PluginService] internal static IPlayerState           PlayerState     { get; private set; } = null!;
    [PluginService] internal static IMarketBoard           MarketBoard     { get; private set; } = null!;
    [PluginService] internal static IGameInventory         GameInventory   { get; private set; } = null!;
    [PluginService] internal static IFramework             Framework       { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle        AddonLifecycle  { get; private set; } = null!;
    [PluginService] internal static ITextureProvider       TextureProvider { get; private set; } = null!;
    [PluginService] internal static IChatGui               ChatGui         { get; private set; } = null!;
    [PluginService] internal static IGameGui                GameGui         { get; private set; } = null!;

    public Configuration Configuration { get; }
    public UniversalisClient Universalis { get; }
    public WatchlistPoller Poller { get; }
    public ItemNameProvider Names { get; }
    public TaxRateService TaxRates { get; }
    public JournalService Journal { get; }
    public RetainerService Retainers { get; }
    public ValuationService Valuation { get; }
    public PluginBridge Bridge { get; }
    public GroupService Groups { get; }
    public BuySignalWatcher BuySignal { get; }
    public SellHistoryProbe SellHistoryProbe { get; }
    public TooltipService Tooltip { get; }
    public CraftingService Crafting { get; }
    public BackupService Backup { get; }

    private Dictionary<string, PriceDsl.INode>? _compiledCustomCache;
    private string _compiledCustomKey = "";

    /// Cached compilation of Configuration.CustomSources. Recompiled when the source set changes.
    public IReadOnlyDictionary<string, PriceDsl.INode> CompiledCustomSources()
    {
        var key = string.Join(";", Configuration.CustomSources.Select(kv => kv.Key + "=" + kv.Value));
        if (_compiledCustomCache == null || key != _compiledCustomKey)
        {
            _compiledCustomCache = PriceDsl.CompileCustomSources(Configuration.CustomSources);
            _compiledCustomKey = key;
        }
        return _compiledCustomCache;
    }

    /// Content-hash of the compiled custom sources. Downstream compiled-formula caches must include
    /// this in their key, otherwise a user's edit of a custom source's formula body won't invalidate.
    public string CustomSourcesFingerprint => _compiledCustomKey;
    public DateTime SessionStartUtc { get; } = DateTime.UtcNow;

    /// Current home world name (e.g. "Midgardsormr"), or null if not logged in / not resolvable.
    /// All sell-price / undercut logic reads this to scope Universalis listings to your realm.
    public string? HomeWorldName
    {
        get
        {
            try
            {
                if (PlayerState.IsLoaded)
                {
                    var name = PlayerState.HomeWorld.ValueNullable?.Name.ExtractText();
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
            }
            catch { /* fall through to tax service cache */ }
            return TaxRates.HomeWorldName;
        }
    }

    /// "Name@World" for the current character, or "" if not logged in / not resolvable.
    /// Used to tag journal entries so Multi-character rollups can slice by owner.
    /// IPlayerState in Dalamud 15 doesn't expose the character name directly, so we reach into
    /// FFXIVClientStructs' PlayerState singleton for it.
    public unsafe string CurrentCharacterKey()
    {
        try
        {
            if (!PlayerState.IsLoaded) return "";
            var world = PlayerState.HomeWorld.ValueNullable?.Name.ExtractText();
            var ps = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance();
            var name = ps != null ? ps->CharacterNameString : null;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(world)) return "";
            return $"{name}@{world}";
        }
        catch { return ""; }
    }

    // Convenience for UI code — the static injected services, reachable as instance members.
    public ITextureProvider TextureProviderRef => TextureProvider;
    public IPluginLog LogRef => Log;

    // Correlate PurchaseRequested (has price) with ItemPurchased (has confirmation).
    private readonly ConcurrentDictionary<(uint ItemId, uint Qty), IMarketBoardPurchaseHandler> _pendingBuys = new();

    private readonly WindowSystem _windowSystem = new("KupoTradeMaster");
    private readonly MainWindow _mainWindow;
    private readonly TooltipOverlay _tooltipOverlay;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        var configDir = PluginInterface.GetPluginConfigDirectory();

        Universalis = new UniversalisClient($"KupoTradeMaster/{version} (github.com/foglerk/kupotrademaster)");
        Universalis.AttachDiagnostics((msg, ex) =>
        {
            if (ex != null) Log.Warning(ex, "KupoTradeMaster: {Msg}", msg);
            else            Log.Warning("KupoTradeMaster: {Msg}", msg);
        });
        Names       = new ItemNameProvider(DataManager);
        Poller      = new WatchlistPoller(Universalis, Configuration, Log);
        TaxRates    = new TaxRateService(Universalis, Configuration, PlayerState, Log);
        Journal     = new JournalService(configDir, Log);
        Bridge      = new PluginBridge(PluginInterface, Log);
        Groups      = new GroupService(Configuration);
        Retainers   = new RetainerService(AddonLifecycle, GameInventory, Framework, Bridge, configDir, Log);
        SellHistoryProbe = new SellHistoryProbe(AddonLifecycle, Log);
        BuySignal   = new BuySignalWatcher(this, Poller, Configuration, Groups, Names, ChatGui, Log);
        Valuation   = new ValuationService(Universalis, Configuration, GameInventory, Framework, PlayerState, Retainers, configDir, Log);
        Tooltip     = new TooltipService(GameGui, Framework, Universalis, Poller, Configuration, Log);
        Crafting    = new CraftingService(DataManager);
        Backup      = new BackupService(Configuration, configDir, Log)
        {
            Journal   = Journal,
            Valuation = Valuation,
            Retainers = Retainers,
        };

        Poller.AttachPlugin(this);

        _mainWindow = new MainWindow(this);
        _tooltipOverlay = new TooltipOverlay(this);
        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(_tooltipOverlay);
        _tooltipOverlay.IsOpen = true;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle KupoTradeMaster. Pass an item name (e.g. /kupo dark matter) to look it up on Universalis.",
        });
        CommandManager.AddHandler(CommandAlt,  new CommandInfo(OnCommand)
        {
            HelpMessage = "Alias for /kupo.",
        });

        PluginInterface.UiBuilder.Draw         += _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi   += ToggleMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainWindow;

        MarketBoard.PurchaseRequested += OnPurchaseRequested;
        MarketBoard.ItemPurchased     += OnItemPurchased;
        Retainers.ListingShrank       += OnRetainerListingShrank;

        Poller.Start();
        TaxRates.Start();
        Valuation.Start();
        Log.Information("KupoTradeMaster loaded — /kupo to open.");
    }

    private void OnPurchaseRequested(IMarketBoardPurchaseHandler handler)
    {
        _pendingBuys[(handler.CatalogId, handler.ItemQuantity)] = handler;
    }

    private void OnItemPurchased(IMarketBoardPurchase confirmation)
    {
        if (!_pendingBuys.TryRemove((confirmation.CatalogId, confirmation.ItemQuantity), out var handler))
        {
            Log.Debug("KupoTradeMaster: purchase confirmation with no matching pending handler ({Id} x{Qty})",
                confirmation.CatalogId, confirmation.ItemQuantity);
            return;
        }

        var qty = (int)confirmation.ItemQuantity;
        var total = (long)handler.PricePerUnit * qty;

        Journal.Append(new JournalEntry(
            TimestampUtc: DateTime.UtcNow,
            Kind: JournalEntryKind.Buy,
            ItemId: handler.CatalogId,
            Quantity: qty,
            UnitPriceGil: handler.PricePerUnit,
            TotalGil: total,
            TaxGil: handler.TotalTax,
            IsHq: handler.IsHq,
            Notes: null,
            CharacterKey: CurrentCharacterKey()));

        Log.Info("KupoTradeMaster: logged buy {Name} x{Qty} @ {Price:N0} = {Total:N0}",
            Names.GetName(handler.CatalogId), qty, handler.PricePerUnit, total);
    }

    private void OnRetainerListingShrank(RetainerService.DetectedListingShrink e)
    {
        // Prefer the retainer's own last-known listing price (the actual asking price); fall back to
        // Universalis min if we never captured the listing price (e.g. sell-list screen never opened).
        var (unit, source) = e.LastKnownUnitPrice > 0
            ? (e.LastKnownUnitPrice, "your listing price")
            : (Valuation.GetCachedMinPrice(e.ItemId), "Universalis min");

        var total = unit * e.QtyDelta;
        var tax = (long)(total * Configuration.TaxRate);
        var note = unit > 0
            ? $"[estimated] listing on {e.RetainerName} shrank; price ≈ {source}"
            : $"[estimated, price unknown] listing on {e.RetainerName} shrank; enter price manually";

        Journal.Append(new JournalEntry(
            TimestampUtc: e.AtUtc,
            Kind: JournalEntryKind.Sell,
            ItemId: e.ItemId,
            Quantity: e.QtyDelta,
            UnitPriceGil: unit,
            TotalGil: total,
            TaxGil: tax,
            IsHq: e.IsHq,
            Notes: note,
            IsEstimated: true,
            CharacterKey: CurrentCharacterKey()));

        Log.Info("KupoTradeMaster: estimated sell — {Name} x{Qty} @ ~{Unit:N0} ({Retainer}, {Src})",
            Names.GetName(e.ItemId), e.QtyDelta, unit, e.RetainerName, source);
    }

    private void OnCommand(string command, string args)
    {
        var query = args?.Trim() ?? "";
        if (query.Length == 0)
        {
            ToggleMainWindow();
            return;
        }

        // `/kupo <name>` — jumps to the top Lumina match: opens KupoTradeMaster, adds a quick highlight, opens Universalis.
        var top = Names.Search(query, 1).FirstOrDefault();
        if (top.Id == 0)
        {
            ChatGui.Print($"[KupoTradeMaster] no items match \"{query}\"");
            _mainWindow.IsOpen = true;
            return;
        }
        ChatGui.Print($"[KupoTradeMaster] opening Universalis for {top.Name} (id {top.Id})");
        UI.ItemContextMenu.OpenUniversalis(top.Id, Configuration.DataScope, Log);
        _mainWindow.IsOpen = true;
    }

    private void ToggleMainWindow() => _mainWindow.Toggle();

    public void Dispose()
    {
        MarketBoard.PurchaseRequested -= OnPurchaseRequested;
        MarketBoard.ItemPurchased     -= OnItemPurchased;
        Retainers.ListingShrank       -= OnRetainerListingShrank;

        PluginInterface.UiBuilder.Draw         -= _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi   -= ToggleMainWindow;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainWindow;

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandAlt);

        _windowSystem.RemoveAllWindows();
        _mainWindow.Dispose();
        _tooltipOverlay.Dispose();
        Tooltip.Dispose();
        Valuation.Dispose();
        BuySignal.Dispose();
        SellHistoryProbe.Dispose();
        Retainers.Dispose();
        Journal.Dispose();
        TaxRates.Dispose();
        Poller.Dispose();
        Universalis.Dispose();
    }
}
