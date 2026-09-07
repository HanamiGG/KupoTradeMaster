using System.Collections.Generic;
using System.Linq;

namespace KupoTradeMaster.Services;

/// Thin wrapper over Configuration.Groups. Keeps the config schema simple and centralizes save calls.
public sealed class GroupService
{
    public const string AllFilter = "All";

    private readonly Configuration _config;

    public GroupService(Configuration config) => _config = config;

    public IEnumerable<string> Names => _config.Groups.Keys.OrderBy(k => k);

    public IEnumerable<string> AllFilterOptions
    {
        get
        {
            yield return AllFilter;
            foreach (var n in Names) yield return n;
        }
    }

    public IReadOnlyList<uint> Members(string groupName)
    {
        if (!_config.Groups.TryGetValue(groupName, out var list)) return System.Array.Empty<uint>();
        return list;
    }

    public IEnumerable<uint> FilteredWatchlist(string filter)
    {
        if (filter == AllFilter || !_config.Groups.TryGetValue(filter, out var list))
            return _config.Watchlist.Distinct();
        var set = new HashSet<uint>(list);
        return _config.Watchlist.Distinct().Where(set.Contains);
    }

    public bool Create(string name)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name) || name == AllFilter) return false;
        lock (_config.Sync)
        {
            if (_config.Groups.ContainsKey(name)) return false;
            _config.Groups[name] = new List<uint>();
        }
        _config.Save();
        return true;
    }

    public bool Rename(string oldName, string newName)
    {
        newName = (newName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName == AllFilter) return false;
        lock (_config.Sync)
        {
            if (!_config.Groups.TryGetValue(oldName, out var list)) return false;
            if (_config.Groups.ContainsKey(newName)) return false;
            _config.Groups.Remove(oldName);
            _config.Groups[newName] = list;
            // Carry associated per-group formula + operation to the new name so users don't
            // silently lose their configuration when they just wanted to rename.
            if (_config.GroupFormulas.Remove(oldName, out var f))
                _config.GroupFormulas[newName] = f;
            if (_config.GroupOperations.Remove(oldName, out var op))
                _config.GroupOperations[newName] = op;
        }
        _config.Save();
        return true;
    }

    public bool Delete(string name)
    {
        bool removed;
        lock (_config.Sync)
        {
            removed = _config.Groups.Remove(name);
            if (removed)
            {
                // Clean up companion entries — otherwise a future group with the same name would
                // silently inherit the stale formula/operation from the deleted one.
                _config.GroupFormulas.Remove(name);
                _config.GroupOperations.Remove(name);
            }
        }
        if (removed) _config.Save();
        return removed;
    }

    public bool AddItem(string group, uint itemId)
    {
        lock (_config.Sync)
        {
            if (!_config.Groups.TryGetValue(group, out var list)) return false;
            if (list.Contains(itemId)) return false;
            list.Add(itemId);
            // Auto-add to master watchlist too, so the group filter actually shows the item.
            if (!_config.Watchlist.Contains(itemId)) _config.Watchlist.Add(itemId);
        }
        _config.Save();
        return true;
    }

    public bool RemoveItem(string group, uint itemId)
    {
        bool removed;
        lock (_config.Sync)
        {
            if (!_config.Groups.TryGetValue(group, out var list)) return false;
            removed = list.Remove(itemId);
        }
        if (removed) _config.Save();
        return removed;
    }

    public IEnumerable<string> GroupsContaining(uint itemId)
        => _config.Groups.Where(kv => kv.Value.Contains(itemId)).Select(kv => kv.Key);

    public string GetFormula(string group)
    {
        if (group == AllFilter) return _config.BuyFormula;

        // Precedence: attached Operation.BuyFormula > per-group GroupFormulas > global BuyFormula.
        // Operations are a named-preset layer above the raw DSL — users can share a "cheap consumables"
        // preset across many groups without duplicating the formula.
        if (_config.GroupOperations.TryGetValue(group, out var opName)
            && _config.Operations.TryGetValue(opName, out var op)
            && !string.IsNullOrWhiteSpace(op.BuyFormula))
            return op.BuyFormula;

        return _config.GroupFormulas.TryGetValue(group, out var f) && !string.IsNullOrWhiteSpace(f)
            ? f
            : _config.BuyFormula;
    }

    public void SetFormula(string group, string formula)
    {
        lock (_config.Sync)
        {
            if (group == AllFilter)
            {
                _config.BuyFormula = formula ?? "";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(formula)) _config.GroupFormulas.Remove(group);
                else _config.GroupFormulas[group] = formula;
            }
        }
        _config.Save();
    }
}
