using System;

namespace KupoTradeMaster.Services;

public readonly record struct FlipScoreInputs(
    double MinPriceGil,
    double SellReferenceGil,
    double VelocityPerDay,
    double FreshnessMinutes,
    double TaxRate);

public readonly record struct FlipScoreResult(
    double Score,
    double MarginGil,
    double PostTaxMarginGil,
    double VelocityFactor,
    double FreshnessFactor);

public static class FlipScorer
{
    // Universalis freshness is noisy under a few minutes — floor it.
    private const double FreshnessFloorMinutes = 5.0;

    public static FlipScoreResult Compute(FlipScoreInputs inputs)
    {
        if (inputs.MinPriceGil <= 0 || inputs.SellReferenceGil <= 0)
            return default;

        var postTaxSell = inputs.SellReferenceGil * (1.0 - inputs.TaxRate);
        var margin = postTaxSell - inputs.MinPriceGil;
        if (margin <= 0)
            return new FlipScoreResult(0, margin, margin, 0, 0);

        var velocityFactor = Math.Sqrt(Math.Max(0.0, inputs.VelocityPerDay));
        var freshnessFactor = 1.0 / Math.Max(inputs.FreshnessMinutes, FreshnessFloorMinutes);
        var score = margin * velocityFactor * freshnessFactor;

        return new FlipScoreResult(score, inputs.SellReferenceGil - inputs.MinPriceGil, margin, velocityFactor, freshnessFactor);
    }

    // Session-relative 0..100 normalization for display.
    public static double Normalize(double raw, double sessionMax)
        => sessionMax <= 0 ? 0 : Math.Clamp(raw / sessionMax * 100.0, 0.0, 100.0);
}
