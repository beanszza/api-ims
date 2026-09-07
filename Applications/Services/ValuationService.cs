using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class ValuationService : IValuationService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<ValuationService> _logger;

    public ValuationService(ScmDbContext context, ILogger<ValuationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<InventoryValuationReportResponse>> GetValuationReportAsync(int? locationId = null, int? categoryId = null)
    {
        try
        {
            var query = _context.InventoryLots
                .Include(l => l.Item).ThenInclude(i => i!.Category)
                .Include(l => l.Item).ThenInclude(i => i!.Uom)
                .Include(l => l.Location)
                .Where(l => l.Status == LotStatus.Available && l.QuantityRemaining > 0)
                .AsQueryable();

            if (locationId.HasValue && locationId.Value > 0)
                query = query.Where(l => l.LocationId == locationId.Value);

            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(l => l.Item!.CategoryId == categoryId.Value);

            var lots = await query.ToListAsync();

            var totalValuation = lots.Sum(l => l.QuantityRemaining * l.UnitCost);

            // Group by Category
            var byCategory = lots
                .GroupBy(l => new { l.Item!.CategoryId, CategoryName = l.Item.Category?.CategoryName ?? "Uncategorized" })
                .Select(g =>
                {
                    var catVal = g.Sum(l => l.QuantityRemaining * l.UnitCost);
                    return new CategoryValuationSummary
                    {
                        CategoryId = g.Key.CategoryId,
                        CategoryName = g.Key.CategoryName,
                        TotalValue = catVal,
                        PercentageOfTotal = totalValuation > 0 ? Math.Round((catVal / totalValuation) * 100m, 2) : 0m
                    };
                })
                .OrderByDescending(c => c.TotalValue)
                .ToList();

            // Group by Location
            var byLocation = lots
                .GroupBy(l => new { l.LocationId, LocationName = l.Location?.LocationName ?? $"Location {l.LocationId}" })
                .Select(g => new LocationValuationSummary
                {
                    LocationId = g.Key.LocationId,
                    LocationName = g.Key.LocationName,
                    TotalValue = g.Sum(l => l.QuantityRemaining * l.UnitCost),
                    LotsCount = g.Count()
                })
                .OrderByDescending(l => l.TotalValue)
                .ToList();

            // Group by Item
            var items = lots
                .GroupBy(l => new
                {
                    l.ItemId,
                    ItemName = l.Item?.ItemName ?? $"Item {l.ItemId}",
                    CategoryName = l.Item?.Category?.CategoryName ?? "Uncategorized",
                    UomName = l.Item?.Uom?.Name ?? "pcs"
                })
                .Select(g =>
                {
                    var totalQty = g.Sum(l => l.QuantityRemaining);
                    var itemVal = g.Sum(l => l.QuantityRemaining * l.UnitCost);
                    var mac = totalQty > 0 ? itemVal / totalQty : 0m;
                    return new ItemValuationDetail
                    {
                        ItemId = g.Key.ItemId,
                        ItemName = g.Key.ItemName,
                        CategoryName = g.Key.CategoryName,
                        OnHandQuantity = totalQty,
                        UomName = g.Key.UomName,
                        MovingAverageUnitCost = Math.Round(mac, 4),
                        TotalValue = itemVal,
                        LotsCount = g.Count()
                    };
                })
                .OrderByDescending(i => i.TotalValue)
                .ToList();

            var report = new InventoryValuationReportResponse
            {
                AsOfDate = DateTime.UtcNow,
                TotalValuation = totalValuation,
                TotalLotsCount = lots.Count,
                ByCategory = byCategory,
                ByLocation = byLocation,
                Items = items
            };

            return ApiResponse<InventoryValuationReportResponse>.SuccessResponse(
                report,
                $"Inventory valuation report generated. Total valuation: ₱{totalValuation:N2} across {lots.Count} active lots.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating valuation report");
            return ApiResponse<InventoryValuationReportResponse>.FailureResponse($"Failed to generate valuation report: {ex.Message}");
        }
    }
}
