using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Dalamud.Plugin.Services;

namespace KupoTradeMaster.Services;

/// One-click bundle of the plugin's live state — Configuration + journal + net-worth history —
/// into a single zip. Restore overwrites journal/history/retainers files; config.json is intentionally
/// skipped on import because a live Configuration instance is already loaded in-process.
public sealed class BackupService
{
    private readonly Configuration _config;
    private readonly string _configDir;
    private readonly IPluginLog _log;

    // Reload targets — set after Plugin construction so Import can rebuild in-memory state to match
    // the newly-restored files. Optional to keep BackupService constructable in isolation.
    public JournalService? Journal { get; set; }
    public ValuationService? Valuation { get; set; }
    public RetainerService? Retainers { get; set; }

    public BackupService(Configuration config, string configDir, IPluginLog log)
    {
        _config = config;
        _configDir = configDir;
        _log = log;
    }

    public string Export(string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? ".");
        if (File.Exists(targetPath)) File.Delete(targetPath);

        using var zip = ZipFile.Open(targetPath, ZipArchiveMode.Create);

        var cfgEntry = zip.CreateEntry("config.json");
        using (var w = new StreamWriter(cfgEntry.Open()))
            w.Write(JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));

        TryAddFile(zip, Path.Combine(_configDir, "journal.jsonl"), "journal.jsonl");
        TryAddFile(zip, Path.Combine(_configDir, "history.jsonl"), "history.jsonl");
        TryAddFile(zip, Path.Combine(_configDir, "retainers.json"), "retainers.json");

        _log.Info("KupoTradeMaster: backup written to {Path}", targetPath);
        return targetPath;
    }

    public void Import(string sourcePath)
    {
        // Route each restored file through its owning service's file-locked ImportFrom so a
        // concurrent capture/append/save can't race File.Create and corrupt the restored file.
        // The service reloads its in-memory state under the same lock immediately after.
        var restored = new System.Collections.Generic.HashSet<string>();
        using var zip = ZipFile.OpenRead(sourcePath);
        foreach (var e in zip.Entries)
        {
            using var src = e.Open();
            switch (e.Name)
            {
                case "journal.jsonl":
                    if (Journal != null)   { Journal.ImportFrom(src);   restored.Add(e.Name); }
                    break;
                case "history.jsonl":
                    if (Valuation != null) { Valuation.ImportHistoryFrom(src); restored.Add(e.Name); }
                    break;
                case "retainers.json":
                    if (Retainers != null) { Retainers.ImportFrom(src); restored.Add(e.Name); }
                    break;
            }
        }

        _log.Info("KupoTradeMaster: restored {N} data files from {Path} (config.json ignored)", restored.Count, sourcePath);
    }

    private static void TryAddFile(ZipArchive zip, string path, string entryName)
    {
        if (!File.Exists(path)) return;
        zip.CreateEntryFromFile(path, entryName);
    }
}
