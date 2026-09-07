using System.Collections.Generic;
using System.Linq;

namespace KupoTradeMaster.Services;

public static class CrossWorldAnalyzer
{
    /// Group listings by world and return per-world minimum price.
    /// Returns a list sorted ascending by min price.
    public static IReadOnlyList<(string World, int MinPrice, int TotalUnits)> PerWorldMins(UniversalisItem item)
    {
        return item.Listings
            .Where(l => !string.IsNullOrEmpty(l.WorldName) && l.PricePerUnit > 0)
            .GroupBy(l => l.WorldName!)
            .Select(g => (
                World: g.Key,
                MinPrice: g.Min(l => l.PricePerUnit),
                TotalUnits: g.Sum(l => l.Quantity)))
            .OrderBy(w => w.MinPrice)
            .ToArray();
    }
}
