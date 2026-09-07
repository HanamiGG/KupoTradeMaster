using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace KupoTradeMaster.Services;

public sealed class ItemNameProvider
{
    private readonly IDataManager _data;
    private readonly ConcurrentDictionary<uint, ItemMeta> _cache = new();
    private readonly object _indexLock = new();
    private List<SearchEntry>? _searchIndex;

    public ItemNameProvider(IDataManager data) => _data = data;

    public string GetName(uint id) => Lookup(id).Name;

    public ushort GetIconId(uint id) => Lookup(id).IconId;

    public bool IsUntradable(uint id) => Lookup(id).Untradable;

    public uint GetVendorPrice(uint id) => Lookup(id).VendorPrice;

    /// Returns up to `maxResults` marketable items whose name contains `query` (case-insensitive).
    public IEnumerable<(uint Id, string Name, ushort IconId)> Search(string query, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2) yield break;
        EnsureIndexBuilt();
        var q = query.ToLowerInvariant();
        var idx = _searchIndex!;
        var yielded = 0;
        foreach (var e in idx)
        {
            if (e.NameLower.Contains(q, StringComparison.Ordinal))
            {
                yield return (e.Id, e.Name, e.IconId);
                if (++yielded >= maxResults) yield break;
            }
        }
    }

    private void EnsureIndexBuilt()
    {
        if (_searchIndex != null) return;
        lock (_indexLock)
        {
            if (_searchIndex != null) return;
            var sheet = _data.GetExcelSheet<Item>();
            var list = new List<SearchEntry>(capacity: 8000);
            if (sheet != null)
            {
                foreach (var row in sheet)
                {
                    // Don't filter by IsUntradable — that flag misclassifies plenty of common tradable items.
                    // If a user adds a genuinely untradable item to their watchlist, Universalis will return
                    // empty and it just shows "—" in the table. Better than hiding real gear from search.
                    var name = row.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    list.Add(new SearchEntry(row.RowId, name, name.ToLowerInvariant(), row.Icon));
                }
            }
            _searchIndex = list;
        }
    }

    private ItemMeta Lookup(uint id)
    {
        return _cache.GetOrAdd(id, i =>
        {
            var sheet = _data.GetExcelSheet<Item>();
            if (sheet == null || !sheet.TryGetRow(i, out var row))
                return new ItemMeta($"#{i}", 0, true, 0);
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) name = $"#{i}";
            return new ItemMeta(name, row.Icon, row.IsUntradable, row.PriceLow);
        });
    }

    private readonly record struct ItemMeta(string Name, ushort IconId, bool Untradable, uint VendorPrice);
    private readonly record struct SearchEntry(uint Id, string Name, string NameLower, ushort IconId);
}
