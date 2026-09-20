using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Applications.Interfaces;

public sealed record StockRequirement(int ItemId, int LocationId, decimal RequiredQuantity);

public sealed record LotAllocation(
    int LotId,
    string LotCode,
    decimal QuantityToDraw,
    DateOnly? ExpiryDate,
    decimal UnitCost);

public sealed record AllocationResult(
    int ItemId,
    int LocationId,
    decimal RequiredQuantity,
    decimal AllocatedQuantity,
    decimal ShortfallQuantity,
    bool IsFulfilled,
    IReadOnlyList<LotAllocation> Allocations);

public interface IAllocationService
{
    /// <summary>
    /// Allocates stock for a single item requirement using First-Expired-First-Out (FEFO).
    /// </summary>
    Task<AllocationResult> AllocateFefoAsync(int itemId, int locationId, decimal requiredQuantity);

    /// <summary>
    /// Allocates stock across multiple ingredient requirements (e.g. for a production recipe batch or transfer).
    /// </summary>
    Task<IReadOnlyList<AllocationResult>> AllocateFefoMultiAsync(IReadOnlyList<StockRequirement> requirements);
}
