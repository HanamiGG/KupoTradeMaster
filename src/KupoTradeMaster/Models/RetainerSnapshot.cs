using System;
using System.Collections.Generic;

namespace KupoTradeMaster.Models;

public sealed record RetainerSnapshot(
    ulong RetainerId,
    string Name,
    long Gil,
    Dictionary<uint, long> Inventory,
    List<RetainerListingSnapshot> Listings,
    DateTime LastSeenUtc);

public sealed record RetainerListingSnapshot(
    uint ItemId,
    int Quantity,
    bool IsHq,
    long UnitPriceGil,      // 0 = price not yet captured (addon hook TODO)
    DateTime LastSeenUtc);
