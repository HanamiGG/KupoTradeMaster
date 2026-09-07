using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KupoTradeMaster.Services;

public sealed class UniversalisMultiItemResponse
{
    [JsonPropertyName("itemIDs")]
    public List<uint> ItemIds { get; set; } = new();

    [JsonPropertyName("items")]
    public Dictionary<string, UniversalisItem> Items { get; set; } = new();

    [JsonPropertyName("worldID")]
    public int? WorldId { get; set; }

    [JsonPropertyName("dcName")]
    public string? DcName { get; set; }

    [JsonPropertyName("regionName")]
    public string? RegionName { get; set; }
}

public sealed class UniversalisItem
{
    [JsonPropertyName("itemID")]
    public uint ItemId { get; set; }

    [JsonPropertyName("lastUploadTime")]
    public long LastUploadTimeMs { get; set; }

    [JsonPropertyName("listings")]
    public List<UniversalisListing> Listings { get; set; } = new();

    [JsonPropertyName("recentHistory")]
    public List<UniversalisSale> RecentHistory { get; set; } = new();

    [JsonPropertyName("currentAveragePrice")]
    public double CurrentAveragePrice { get; set; }

    [JsonPropertyName("currentAveragePriceNQ")]
    public double CurrentAveragePriceNq { get; set; }

    [JsonPropertyName("currentAveragePriceHQ")]
    public double CurrentAveragePriceHq { get; set; }

    [JsonPropertyName("regularSaleVelocity")]
    public double RegularSaleVelocity { get; set; }

    [JsonPropertyName("nqSaleVelocity")]
    public double NqSaleVelocity { get; set; }

    [JsonPropertyName("hqSaleVelocity")]
    public double HqSaleVelocity { get; set; }

    [JsonPropertyName("averagePrice")]
    public double AveragePrice { get; set; }

    [JsonPropertyName("minPrice")]
    public int MinPrice { get; set; }

    [JsonPropertyName("minPriceNQ")]
    public int MinPriceNq { get; set; }

    [JsonPropertyName("minPriceHQ")]
    public int MinPriceHq { get; set; }

    [JsonPropertyName("maxPrice")]
    public int MaxPrice { get; set; }
}

public sealed class UniversalisListing
{
    [JsonPropertyName("pricePerUnit")]
    public int PricePerUnit { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("worldName")]
    public string? WorldName { get; set; }

    [JsonPropertyName("worldID")]
    public int? WorldId { get; set; }

    [JsonPropertyName("hq")]
    public bool Hq { get; set; }

    [JsonPropertyName("retainerName")]
    public string? RetainerName { get; set; }

    [JsonPropertyName("lastReviewTime")]
    public long LastReviewTimeSeconds { get; set; }
}

public sealed class UniversalisSale
{
    [JsonPropertyName("pricePerUnit")]
    public int PricePerUnit { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("hq")]
    public bool Hq { get; set; }

    [JsonPropertyName("worldName")]
    public string? WorldName { get; set; }

    [JsonPropertyName("timestamp")]
    public long TimestampSeconds { get; set; }

    [JsonPropertyName("buyerName")]
    public string? BuyerName { get; set; }
}
