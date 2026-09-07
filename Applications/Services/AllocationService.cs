using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class AllocationService : IAllocationService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<AllocationService> _logger;

    public AllocationService(
        ScmDbContext context,
        ILogger<AllocationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AllocationResult> AllocateFefoAsync(int itemId, int locationId, decimal requiredQuantity)
    {
        if (requiredQuantity <= 0)
        {
            return new AllocationResult(
                ItemId: itemId,
                LocationId: locationId,
                RequiredQuantity: requiredQuantity,
                AllocatedQuantity: 0m,
                ShortfallQuantity: 0m,
                IsFulfilled: true,
                Allocations: Array.Empty<LotAllocation>());
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Fetch available, non-expired lots for the item at the location
        var availableLots = await _context.InventoryLots
            .Where(l => l.ItemId == itemId &&
                        l.LocationId == locationId &&
                        l.Status == LotStatus.Available &&
                        l.QuantityRemaining > 0 &&
                        (l.ExpiryDate == null || l.ExpiryDate >= today))
            .OrderBy(l => l.ExpiryDate == null ? 1 : 0) // Lots with explicit expiry date first
            .ThenBy(l => l.ExpiryDate)                  // Earliest expiry first (FEFO)
            .ThenBy(l => l.ReceivedDate)                // Oldest arrival date next (FIFO tie-breaker)
            .ThenBy(l => l.LotId)
            .ToListAsync();

        var allocations = new List<LotAllocation>();
        decimal remainingNeeded = requiredQuantity;
        decimal totalAllocated = 0m;

        foreach (var lot in availableLots)
        {
            if (remainingNeeded <= 0)
                break;

            decimal draw = Math.Min(lot.QuantityRemaining, remainingNeeded);
            allocations.Add(new LotAllocation(
                LotId: lot.LotId,
                LotCode: lot.LotCode,
                QuantityToDraw: draw,
                ExpiryDate: lot.ExpiryDate,
                UnitCost: lot.UnitCost));

            totalAllocated += draw;
            remainingNeeded -= draw;
        }

        decimal shortfall = Math.Max(0m, requiredQuantity - totalAllocated);
        bool isFulfilled = shortfall == 0m;

        return new AllocationResult(
            ItemId: itemId,
            LocationId: locationId,
            RequiredQuantity: requiredQuantity,
            AllocatedQuantity: totalAllocated,
            ShortfallQuantity: shortfall,
            IsFulfilled: isFulfilled,
            Allocations: allocations);
    }

    public async Task<IReadOnlyList<AllocationResult>> AllocateFefoMultiAsync(IReadOnlyList<StockRequirement> requirements)
    {
        var results = new List<AllocationResult>();

        foreach (var req in requirements)
        {
            var result = await AllocateFefoAsync(req.ItemId, req.LocationId, req.RequiredQuantity);
            results.Add(result);
        }

        return results;
    }
}
