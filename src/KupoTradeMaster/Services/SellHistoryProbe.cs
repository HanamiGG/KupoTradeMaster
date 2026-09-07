using System;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace KupoTradeMaster.Services;

/// Probes the RetainerSellHistory addon and dumps its AtkValues so we can reverse-engineer the row
/// layout. Ships as a debug tool; graduates into a real parser once we know which indices hold
/// itemId / quantity / unit price / total / tax / timestamp for each of the 20 history rows.
public sealed class SellHistoryProbe : IDisposable
{
    private const string SellHistoryAddon = "RetainerSellHistory";
    private static readonly TimeSpan DumpCooldown = TimeSpan.FromSeconds(3);

    private readonly IAddonLifecycle _lifecycle;
    private readonly IPluginLog _log;
    private DateTime _lastDumpUtc = DateTime.MinValue;

    public SellHistoryProbe(IAddonLifecycle lifecycle, IPluginLog log)
    {
        _lifecycle = lifecycle;
        _log = log;
        _lifecycle.RegisterListener(AddonEvent.PostRefresh, SellHistoryAddon, OnAddonEvent);
        _lifecycle.RegisterListener(AddonEvent.PostReceiveEvent, SellHistoryAddon, OnAddonEvent);
    }

    private unsafe void OnAddonEvent(AddonEvent type, AddonArgs args)
    {
        var now = DateTime.UtcNow;
        if (now - _lastDumpUtc < DumpCooldown) return;
        _lastDumpUtc = now;

        try
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            if (addon == null) return;
            var values = addon->AtkValuesSpan;
            var count = values.Length;
            _log.Info("KupoTradeMaster: RetainerSellHistory {Event} — AtkValuesCount={Count}", type, count);

            var sb = new StringBuilder(4096);
            var dumpLen = Math.Min(count, 160);
            for (var i = 0; i < dumpLen; i++)
            {
                var v = values[i];
                sb.Clear();
                sb.Append("  [").Append(i).Append("] ").Append(v.Type).Append(" = ");
                AppendAtkValue(sb, v);
                _log.Info("{Line}", sb.ToString());
            }
            if (count > dumpLen)
                _log.Info("  … {N} more values not dumped", count - dumpLen);
        }
        catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: SellHistoryProbe dump failed"); }
    }

    private static unsafe void AppendAtkValue(StringBuilder sb, AtkValue v)
    {
        switch (v.Type)
        {
            case AtkValueType.Int:     sb.Append(v.Int); break;
            case AtkValueType.UInt:    sb.Append(v.UInt); break;
            case AtkValueType.Bool:    sb.Append(v.Byte != 0); break;
            case AtkValueType.Float:   sb.Append(v.Float); break;
            case AtkValueType.String:
            case AtkValueType.Managed:
                try
                {
                    var addr = (IntPtr)v.String.Value;
                    var s = addr != IntPtr.Zero ? Marshal.PtrToStringUTF8(addr) : null;
                    sb.Append('"').Append(s ?? "").Append('"');
                }
                catch { sb.Append("(string read failed)"); }
                break;
            default: sb.Append("(unhandled: ").Append(v.Type).Append(')'); break;
        }
    }

    public void Dispose() => _lifecycle.UnregisterListener(OnAddonEvent);
}
