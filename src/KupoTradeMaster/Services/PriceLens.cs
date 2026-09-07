namespace KupoTradeMaster.Services;

/// Home-world-scoped price helpers. Universalis returns cross-world listings on a DC-scope fetch;
/// naively taking listings.Min() lets any single foreign world's outlier listing (e.g. Rose Garnet Ore
/// at 2M on some Materia world) contaminate our sell reference. All "what will I sell at" and
/// "am I being undercut" logic goes through here — home world first, DC as safety fallback.
public static class PriceLens
{
    /// Min unit price of listings on the home world (0 if none / home world unknown).
    public static int HomeWorldMin(UniversalisItem item, string? homeWorld, bool hqOnly = false)
    {
        if (string.IsNullOrEmpty(homeWorld) || item.Listings.Count == 0) return 0;
        var min = 0;
        foreach (var l in item.Listings)
        {
            if (l.WorldName != homeWorld) continue;
            if (l.PricePerUnit <= 0) continue;
            if (hqOnly && !l.Hq) continue;
            if (min == 0 || l.PricePerUnit < min) min = l.PricePerUnit;
        }
        return min;
    }

    /// Average listing unit price on the home world (0 if none).
    public static double HomeWorldAvg(UniversalisItem item, string? homeWorld, bool hqOnly = false)
    {
        if (string.IsNullOrEmpty(homeWorld) || item.Listings.Count == 0) return 0;
        long sum = 0;
        var count = 0;
        foreach (var l in item.Listings)
        {
            if (l.WorldName != homeWorld) continue;
            if (l.PricePerUnit <= 0) continue;
            if (hqOnly && !l.Hq) continue;
            sum += l.PricePerUnit;
            count++;
        }
        return count == 0 ? 0 : (double)sum / count;
    }

    /// The min listing price to compare against — home world's first, else DC-wide from listings,
    /// else the API aggregate. hqOnly filters at each stage; if hqOnly is set and no HQ data exists
    /// at any level, returns 0 (unpriced) instead of falling back to NQ.
    public static int EffectiveMin(UniversalisItem item, string? homeWorld, bool hqOnly = false)
    {
        var hw = HomeWorldMin(item, homeWorld, hqOnly);
        if (hw > 0) return hw;

        if (item.Listings.Count > 0)
        {
            var dc = 0;
            foreach (var l in item.Listings)
            {
                if (l.PricePerUnit <= 0) continue;
                if (hqOnly && !l.Hq) continue;
                if (dc == 0 || l.PricePerUnit < dc) dc = l.PricePerUnit;
            }
            if (dc > 0) return dc;
            if (hqOnly) return 0; // don't fall back to NQ aggregates on an HQ query.
        }

        return hqOnly ? item.MinPriceHq : item.MinPrice;
    }

    /// Realistic sell-at price when undercutting: one gil under EffectiveMin
    /// (TSM's `firstlist - 1`). Zero when no listing data exists.
    public static double SellReference(UniversalisItem item, string? homeWorld, bool hqOnly = false)
    {
        var e = EffectiveMin(item, homeWorld, hqOnly);
        return e > 1 ? e - 1 : e;
    }

    /// True when the effective min came from home-world listings, false when it fell back to DC-wide.
    public static bool IsHomeWorldSourced(UniversalisItem item, string? homeWorld, bool hqOnly = false)
        => HomeWorldMin(item, homeWorld, hqOnly) > 0;
}
