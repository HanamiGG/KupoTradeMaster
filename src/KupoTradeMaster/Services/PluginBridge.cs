using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

namespace KupoTradeMaster.Services;

public sealed class PluginBridge
{
    private readonly IDalamudPluginInterface _pi;
    private readonly IPluginLog _log;

    // Detected states
    public bool HasAllaganTools { get; private set; }
    public bool HasAutoRetainer { get; private set; }
    public bool HasArtisan       { get; private set; }

    // Allagan Tools IPC (documented, primitive-typed only)
    private ICallGateSubscriber<bool>? _atIsInitialized;
    private ICallGateSubscriber<ulong>? _atCurrentCharacter;
    private ICallGateSubscriber<bool, HashSet<ulong>>? _atCharactersOwnedByActive;
    private ICallGateSubscriber<uint, ulong, int, uint>? _atItemCount;
    private ICallGateSubscriber<uint, ulong, int, uint>? _atItemCountHq;

    // Artisan status probes (unused for now; keep hooks so we can drive Crafting pillar later)
    private ICallGateSubscriber<bool>? _artisanIsBusy;
    private ICallGateSubscriber<Dictionary<int, string>>? _artisanGetLists;

    // AutoRetainer — returns ECommons types we don't have at compile time; use object + reflection.
    private ICallGateSubscriber<List<ulong>>? _arGetRegisteredCharacters;
    private ICallGateSubscriber<ulong, object?>? _arGetOfflineCharacterData;

    public PluginBridge(IDalamudPluginInterface pi, IPluginLog log)
    {
        _pi = pi;
        _log = log;
        Rescan();
    }

    /// Re-check installed plugins and (re)build IPC subscribers. Cheap to call on demand.
    public void Rescan()
    {
        HasAllaganTools = _pi.InstalledPlugins.Any(p => p.InternalName == "InventoryTools" && p.IsLoaded);
        HasAutoRetainer = _pi.InstalledPlugins.Any(p => p.InternalName == "AutoRetainer" && p.IsLoaded);
        HasArtisan      = _pi.InstalledPlugins.Any(p => p.InternalName == "Artisan"      && p.IsLoaded);

        try
        {
            if (HasAllaganTools)
            {
                _atIsInitialized           = _pi.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
                _atCurrentCharacter        = _pi.GetIpcSubscriber<ulong>("AllaganTools.CurrentCharacter");
                _atCharactersOwnedByActive = _pi.GetIpcSubscriber<bool, HashSet<ulong>>("AllaganTools.GetCharactersOwnedByActive");
                _atItemCount               = _pi.GetIpcSubscriber<uint, ulong, int, uint>("AllaganTools.ItemCount");
                _atItemCountHq             = _pi.GetIpcSubscriber<uint, ulong, int, uint>("AllaganTools.ItemCountHQ");
            }
            else
            {
                _atIsInitialized = null; _atCurrentCharacter = null; _atCharactersOwnedByActive = null;
                _atItemCount = null; _atItemCountHq = null;
            }

            if (HasArtisan)
            {
                _artisanIsBusy   = _pi.GetIpcSubscriber<bool>("Artisan.IsBusy");
                _artisanGetLists = _pi.GetIpcSubscriber<Dictionary<int, string>>("Artisan.GetLists");
            }
            else
            {
                _artisanIsBusy = null; _artisanGetLists = null;
            }

            if (HasAutoRetainer)
            {
                _arGetRegisteredCharacters = _pi.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCharacters");
                _arGetOfflineCharacterData = _pi.GetIpcSubscriber<ulong, object?>("AutoRetainer.GetOfflineCharacterData");
            }
            else
            {
                _arGetRegisteredCharacters = null; _arGetOfflineCharacterData = null;
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "KupoTradeMaster: PluginBridge Rescan failed while wiring IPC");
        }

        _log.Info("KupoTradeMaster: PluginBridge — AllaganTools={AT} AutoRetainer={AR} Artisan={Ar}",
            HasAllaganTools, HasAutoRetainer, HasArtisan);
    }

    /// True once Allagan Tools finishes its own startup.
    public bool AllaganToolsReady()
    {
        if (_atIsInitialized == null) return false;
        try { return _atIsInitialized.InvokeFunc(); }
        catch (IpcError) { return false; }
        catch (Exception) { return false; }
    }

    public ulong? AllaganToolsCurrentCharacter()
    {
        if (_atCurrentCharacter == null) return null;
        try { return _atCurrentCharacter.InvokeFunc(); }
        catch (IpcError) { return null; }
        catch (Exception) { return null; }
    }

    /// Returns retainer/character/FC content IDs owned by the currently-active character.
    /// This gives us the roster BEFORE the user has visited a summoning bell in-session.
    public HashSet<ulong>? AllaganToolsCharactersOwnedByActive(bool includeSelf = false)
    {
        if (_atCharactersOwnedByActive == null) return null;
        try { return _atCharactersOwnedByActive.InvokeFunc(includeSelf); }
        catch (IpcError) { return null; }
        catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: AT GetCharactersOwnedByActive failed"); return null; }
    }

    /// Quantity of an item held by a specific character/retainer (any inventory type = -1). 0 if AT not present.
    public uint AllaganToolsItemCount(uint itemId, ulong characterId, int inventoryType = -1, bool hq = false)
    {
        var sub = hq ? _atItemCountHq : _atItemCount;
        if (sub == null) return 0;
        try { return sub.InvokeFunc(itemId, characterId, inventoryType); }
        catch (IpcError) { return 0; }
        catch (Exception) { return 0; }
    }

    public bool ArtisanIsBusy()
    {
        if (_artisanIsBusy == null) return false;
        try { return _artisanIsBusy.InvokeFunc(); }
        catch (IpcError) { return false; }
        catch (Exception) { return false; }
    }

    /// Pulls per-retainer offline data from AutoRetainer via reflection (since OfflineCharacterData is
    /// an ECommons type we don't have at compile time). Returns empty if AR isn't installed, the IPC
    /// names have changed, or the underlying types differ from what AR historically ships.
    public IReadOnlyList<AutoRetainerOfflineRetainer> AutoRetainerRetainers()
    {
        if (_arGetRegisteredCharacters == null || _arGetOfflineCharacterData == null)
            return Array.Empty<AutoRetainerOfflineRetainer>();

        List<ulong> cids;
        try { cids = _arGetRegisteredCharacters.InvokeFunc(); }
        catch (IpcError) { return Array.Empty<AutoRetainerOfflineRetainer>(); }
        catch (Exception) { return Array.Empty<AutoRetainerOfflineRetainer>(); }

        var result = new List<AutoRetainerOfflineRetainer>();
        foreach (var cid in cids)
        {
            object? data;
            try { data = _arGetOfflineCharacterData.InvokeFunc(cid); }
            catch (Exception) { continue; }
            if (data == null) continue;

            var t = data.GetType();
            var charName = GetMember(t, data, "Name") as string ?? "?";
            var world    = GetMember(t, data, "World") as string ?? "?";
            var retainerData = GetMember(t, data, "RetainerData") as System.Collections.IEnumerable;
            if (retainerData == null) continue;

            foreach (var r in retainerData)
            {
                if (r == null) continue;
                var rt = r.GetType();
                var name       = GetMember(rt, r, "Name") as string ?? "?";
                var retId      = GetMember(rt, r, "RetainerID") is ulong id ? id : 0UL;
                var gilObj     = GetMember(rt, r, "Gil");
                var mbItemsObj = GetMember(rt, r, "MBItems");
                var ventureIdObj  = GetMember(rt, r, "VentureID");
                var ventureEndObj = GetMember(rt, r, "VentureEndsAt");
                var hasVenObj     = GetMember(rt, r, "HasVenture");
                var levelObj      = GetMember(rt, r, "Level");
                var jobObj        = GetMember(rt, r, "Job");

                var gil     = gilObj switch { uint u => (long)u, int i => i, ulong ul => (long)ul, long l => l, _ => 0L };
                var mbItems = mbItemsObj switch { uint u => (int)u, int i => i, ulong ul => (int)ul, _ => 0 };
                var ventureId = ventureIdObj switch { uint u => u, int i => (uint)i, _ => 0u };
                var ventureEnd = ventureEndObj switch { long l => l, int i => i, uint u => u, _ => 0L };
                var hasVenture = hasVenObj switch { bool b => b, _ => ventureId != 0 };
                var level = levelObj switch { byte b => (int)b, int i => i, uint u => (int)u, _ => 0 };
                var job = jobObj switch { byte b => (int)b, int i => i, uint u => (int)u, _ => 0 };

                result.Add(new AutoRetainerOfflineRetainer(retId, name, charName, world, gil, mbItems, ventureId, ventureEnd, hasVenture, level, job));
            }
        }
        return result;
    }

    private static object? GetMember(Type t, object obj, string name)
    {
        var f = t.GetField(name);
        if (f != null) return f.GetValue(obj);
        var p = t.GetProperty(name);
        return p?.GetValue(obj);
    }
}

public sealed record AutoRetainerOfflineRetainer(
    ulong RetainerId, string Name, string OwnerCharacter, string World, long Gil, int MarketItemCount,
    uint VentureId, long VentureEndsAtUnix, bool HasVenture, int Level, int Job);
