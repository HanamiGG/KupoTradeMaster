using System;

namespace KupoTradeMaster.Models;

public enum JournalEntryKind { Buy, Sell }

public sealed record JournalEntry(
    DateTime TimestampUtc,
    JournalEntryKind Kind,
    uint ItemId,
    int Quantity,
    long UnitPriceGil,
    long TotalGil,
    long TaxGil,
    bool IsHq,
    string? Notes,
    bool IsEstimated = false,
    // "Name@World" of the character that owned the buy/sell. Empty for legacy entries pre-Session N2.
    string CharacterKey = "");

public readonly record struct JournalAggregate(int BuyQty, int SellQty, long SpentGil, long EarnedGil, long NetGil);
