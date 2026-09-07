using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Plugin.Services;
using KupoTradeMaster.Models;

namespace KupoTradeMaster.Services;

public sealed class JournalService : IDisposable
{
    private readonly string _path;
    private readonly IPluginLog _log;
    private readonly List<JournalEntry> _entries = new();
    private readonly object _sync = new();
    // Serializes all disk I/O on _path so BackupService.Import's file replace can't race a concurrent
    // Append/Rewrite from the plugin's own event handlers.
    private readonly object _fileLock = new();

    public IReadOnlyList<JournalEntry> Snapshot()
    {
        lock (_sync) return _entries.ToArray();
    }

    public event Action? Changed;

    public JournalService(string configDir, IPluginLog log)
    {
        _log = log;
        Directory.CreateDirectory(configDir);
        _path = Path.Combine(configDir, "journal.jsonl");
        Load();
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            foreach (var line in File.ReadAllLines(_path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var entry = JsonSerializer.Deserialize<JournalEntry>(line);
                if (entry != null) _entries.Add(entry);
            }
            _log.Info("KupoTradeMaster: loaded {N} journal entries from {Path}", _entries.Count, _path);
        }
        catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: journal load failed"); }
    }

    /// Drops the in-memory list and re-reads the file. Called after Backup.Restore overwrites
    /// journal.jsonl so subsequent Appends don't mix restored file contents with stale memory state.
    public void ReloadFromDisk()
    {
        lock (_fileLock)
        lock (_sync)
        {
            _entries.Clear();
            Load();
        }
        Changed?.Invoke();
    }

    /// Overwrite journal.jsonl with the given stream contents, holding the file lock so no Append
    /// can interleave. Called by BackupService.Import — the sanctioned way to replace journal.jsonl.
    public void ImportFrom(System.IO.Stream source)
    {
        lock (_fileLock)
        {
            using var dst = File.Create(_path);
            source.CopyTo(dst);
        }
        ReloadFromDisk();
    }

    public void Append(JournalEntry entry)
    {
        lock (_sync) _entries.Add(entry);
        lock (_fileLock)
        {
            try { File.AppendAllText(_path, JsonSerializer.Serialize(entry) + "\n"); }
            catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: journal append failed"); }
        }
        Changed?.Invoke();
    }

    public void ReplaceEntry(JournalEntry oldEntry, JournalEntry newEntry)
    {
        lock (_sync)
        {
            var idx = _entries.IndexOf(oldEntry);
            if (idx < 0) return;
            _entries[idx] = newEntry;
            Rewrite();
        }
        Changed?.Invoke();
    }

    public void DeleteEntry(JournalEntry entry)
    {
        lock (_sync)
        {
            var removed = _entries.Remove(entry);
            if (!removed) return;
            Rewrite();
        }
        Changed?.Invoke();
    }

    private void Rewrite()
    {
        lock (_fileLock)
        {
            try
            {
                using var w = new StreamWriter(_path, append: false);
                foreach (var e in _entries) w.WriteLine(JsonSerializer.Serialize(e));
            }
            catch (Exception ex) { _log.Warning(ex, "KupoTradeMaster: journal rewrite failed"); }
        }
    }

    public string ExportCsv(string targetPath, Func<uint, string> itemNameOf)
    {
        JournalEntry[] snap;
        lock (_sync) snap = _entries.ToArray();

        using var w = new StreamWriter(targetPath, append: false);
        w.WriteLine("timestamp_utc,kind,item_id,item_name,quantity,unit_price_gil,total_gil,tax_gil,is_hq,is_estimated,notes");
        foreach (var e in snap)
        {
            var name = itemNameOf(e.ItemId).Replace(",", " ").Replace("\"", "'");
            var notes = (e.Notes ?? "").Replace(",", ";").Replace("\"", "'").Replace("\n", " ");
            w.WriteLine($"{e.TimestampUtc:O},{e.Kind},{e.ItemId},\"{name}\",{e.Quantity},{e.UnitPriceGil},{e.TotalGil},{e.TaxGil},{e.IsHq},{e.IsEstimated},\"{notes}\"");
        }
        _log.Info("KupoTradeMaster: exported {N} journal entries to {Path}", snap.Length, targetPath);
        return targetPath;
    }

    public IEnumerable<KeyValuePair<uint, JournalAggregate>> AggregateByItem(DateTime? sinceUtc = null)
    {
        JournalEntry[] snap;
        lock (_sync) snap = _entries.ToArray();

        var filtered = sinceUtc.HasValue
            ? snap.Where(e => e.TimestampUtc >= sinceUtc.Value)
            : snap;

        return filtered.GroupBy(e => e.ItemId)
            .Select(g =>
            {
                var buys  = g.Where(e => e.Kind == JournalEntryKind.Buy).ToArray();
                var sells = g.Where(e => e.Kind == JournalEntryKind.Sell).ToArray();
                var buyQty  = buys.Sum(e => e.Quantity);
                var sellQty = sells.Sum(e => e.Quantity);
                var spent   = buys.Sum(e => e.TotalGil);
                var earned  = sells.Sum(e => e.TotalGil - e.TaxGil);
                return new KeyValuePair<uint, JournalAggregate>(g.Key,
                    new JournalAggregate(buyQty, sellQty, spent, earned, earned - spent));
            });
    }

    public double SessionGilPerHour(DateTime sessionStartUtc)
    {
        var elapsed = (DateTime.UtcNow - sessionStartUtc).TotalHours;
        if (elapsed <= 0) return 0;

        long net;
        lock (_sync)
        {
            net = _entries
                .Where(e => e.TimestampUtc >= sessionStartUtc)
                .Sum(e => e.Kind == JournalEntryKind.Sell
                    ? e.TotalGil - e.TaxGil
                    : -e.TotalGil);
        }
        return net / elapsed;
    }

    public void Dispose() { }
}
