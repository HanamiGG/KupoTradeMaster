using System;

namespace KupoTradeMaster.Models;

public sealed record NetWorthSnapshot(
    DateTime CapturedUtc,
    long GilOnCharacter,
    long GilInRetainers,
    long BagValueGil,
    long SaddlebagValueGil,
    long RetainerInventoryValueGil,
    long ActiveListingsValueGil,
    long TotalGil,
    int UniqueItemsValued);
