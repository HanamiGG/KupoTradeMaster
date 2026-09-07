using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace KupoTradeMaster;

public sealed class Operation
{
    public string BuyFormula { get; set; } = "";
    public string SellFormula { get; set; } = "";
    public int RestockQuantity { get; set; } = 0;
    public int MinRestockQuantity { get; set; } = 0;
    public string Notes { get; set; } = "";
}

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public string DataScope { get; set; } = "Aether";
    public double TaxRate { get; set; } = 0.05;
    public int PerItemMinPollSeconds { get; set; } = 60;

    public List<uint> Watchlist { get; set; } = new()
    {
        8, 9, 10, 11, 12, 13,
        10386, 4870, 4868, 33915,
    };

    public Dictionary<string, List<uint>> Groups { get; set; } = new();
    public string BuyFormula { get; set; } = "";
    public Dictionary<string, string> GroupFormulas { get; set; } = new();
    public bool BuySignalChatNotifications { get; set; } = true;
    public Dictionary<string, string> CustomSources { get; set; } = new();
    public bool TooltipEnabled { get; set; } = true;
    public Dictionary<uint, DateTime> AlertMutedUntilUtc { get; set; } = new();
    public Dictionary<string, Operation> Operations { get; set; } = new();
    public Dictionary<string, string> GroupOperations { get; set; } = new();

    private IDalamudPluginInterface? _pi;

    public void Initialize(IDalamudPluginInterface pi)
    {
        _pi = pi;
        var deduped = Watchlist.Distinct().ToList();
        var dirty = deduped.Count != Watchlist.Count;
        if (dirty) Watchlist = deduped;

        if (TaxRate < 0.03 || TaxRate > 0.10)
        {
            TaxRate = 0.05;
            dirty = true;
        }

        var now = DateTime.UtcNow;
        var expired = AlertMutedUntilUtc.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList();
        if (expired.Count > 0)
        {
            foreach (var id in expired) AlertMutedUntilUtc.Remove(id);
            dirty = true;
        }

        if (dirty) Save();
    }

    /// Guards the mutable collections (Watchlist, AlertMutedUntilUtc, Groups, etc.) against
    /// UI-thread mutation racing background-thread iteration in poller-driven services. Held
    /// briefly around Add/Remove/Set and around read-side snapshots.
    [System.Text.Json.Serialization.JsonIgnore]
    public object Sync { get; } = new();

    public void Save()
    {
        // Serialize under Sync so JsonSerializer's iteration of Watchlist / AlertMutedUntilUtc
        // can't race a concurrent Add/Remove from another thread.
        lock (Sync) _pi?.SavePluginConfig(this);
    }
}
