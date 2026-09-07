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

public class LotService : ILotService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<LotService> _logger;

    public LotService(ScmDbContext context, ILogger<LotService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<PagedData<LotResponse>>> GetAllLotsAsync(
        string? itemName = null,
        string? locationName = null,
        string? status = null,
        int page = 1,
        int pageSize = 10)
    {
        try
        {
            var query = _context.InventoryLots.AsQueryable();

            if (!string.IsNullOrWhiteSpace(itemName))
                query = query.Where(l => l.Item != null && l.Item.ItemName.ToLower().Contains(itemName.ToLower()));

            if (!string.IsNullOrWhiteSpace(locationName))
                query = query.Where(l => l.Location != null && l.Location.LocationName.ToLower().Contains(locationName.ToLower()));

            if (!string.IsNullOrWhiteSpace(status))
            {
                // Parse via EnumDbValue so "Available", "On Hold", etc. all map correctly.
                if (EnumDbValue.TryParse<LotStatus>(status, out var parsedStatus))
                    query = query.Where(l => l.Status == parsedStatus);
                else
                    return ApiResponse<PagedData<LotResponse>>.FailureResponse(
                        $"Invalid status '{status}'. Accepted values: {EnumDbValue.DescribeAccepted<LotStatus>()}.");
            }

            var totalCount = await query.CountAsync();

            // FEFO order: lots expiring soonest first; non-expiring lots go last, ordered by receipt date.
            // Materialise before projection: the EnumDbValue conversion for SourceType and Status has no
            // SQL translation and must run client-side.
            var rawLots = await query
                .Include(l => l.Item)
                .Include(l => l.Location)
                .Include(l => l.Supplier)
                .Include(l => l.Uom)
                .OrderBy(l => l.ExpiryDate == null ? 1 : 0)      // nulls last
                .ThenBy(l => l.ExpiryDate)                        // earliest expiry first
                .ThenBy(l => l.ReceivedDate)                      // FIFO fallback
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var lots = rawLots.Select(MapToResponse).ToList();

            var pagedData = new PagedData<LotResponse>
            {
                Items = lots,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return ApiResponse<PagedData<LotResponse>>.SuccessResponse(pagedData, "Lots retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching lots.");
            return ApiResponse<PagedData<LotResponse>>.FailureResponse("An error occurred while retrieving lot data.");
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<List<LotResponse>>> GetLotsByItemAsync(int itemId)
    {
        try
        {
            // Confirm the item exists.
            var itemExists = await _context.Items.AnyAsync(i => i.ItemId == itemId);
            if (!itemExists)
                return ApiResponse<List<LotResponse>>.FailureResponse($"Item {itemId} not found.");

            var rawLots = await _context.InventoryLots
                .Where(l => l.ItemId == itemId)
                .Include(l => l.Item)
                .Include(l => l.Location)
                .Include(l => l.Supplier)
                .Include(l => l.Uom)
                // FEFO sort: Available lots with an expiry date first (soonest), then no-expiry
                // Available, then non-available lots by status/received for context.
                .OrderBy(l => l.Status != LotStatus.Available ? 1 : 0)   // Available first
                .ThenBy(l => l.ExpiryDate == null ? 1 : 0)               // expiring before non-expiring
                .ThenBy(l => l.ExpiryDate)                                // earliest expiry
                .ThenBy(l => l.ReceivedDate)                              // FIFO tiebreaker
                .ToListAsync();

            if (rawLots.Count == 0)
                return ApiResponse<List<LotResponse>>.SuccessResponse([], "No lots found for this item.");

            // Compute total available quantity for SharePercent.
            var totalAvailable = rawLots
                .Where(l => l.Status == LotStatus.Available)
                .Sum(l => l.QuantityRemaining);

            // The FEFO-consume-next lot is the first Available lot after FEFO sort.
            var fefoNextLotId = rawLots.FirstOrDefault(l => l.Status == LotStatus.Available)?.LotId;

            var responses = rawLots.Select(l =>
            {
                var r = MapToResponse(l);
                r.IsFefoNext = l.LotId == fefoNextLotId;
                r.SharePercent = (l.Status == LotStatus.Available && totalAvailable > 0)
                    ? Math.Round(l.QuantityRemaining / totalAvailable * 100, 1)
                    : null;
                return r;
            }).ToList();

            return ApiResponse<List<LotResponse>>.SuccessResponse(responses, $"{responses.Count} lot(s) found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching lots for item {ItemId}.", itemId);
            return ApiResponse<List<LotResponse>>.FailureResponse("An error occurred while retrieving lot data.");
        }
    }

    private static LotResponse MapToResponse(Domains.Entities.InventoryLot l) => new()
    {
        LotId = l.LotId,
        LotCode = l.LotCode,
        ItemId = l.ItemId,
        ItemName = l.Item?.ItemName ?? "Unknown Item",
        LocationId = l.LocationId,
        LocationName = l.Location?.LocationName ?? "Unknown Location",
        SourceType = EnumDbValue.ToDbValue(l.SourceType),
        SupplierId = l.SupplierId,
        SupplierName = l.Supplier?.CompanyName,
        SupplierLotNo = l.SupplierLotNo,
        ReceivedDate = l.ReceivedDate,
        ManufactureDate = l.ManufactureDate,
        ExpiryDate = l.ExpiryDate,
        QuantityReceived = l.QuantityReceived,
        QuantityRemaining = l.QuantityRemaining,
        UomName = l.Uom?.Abbreviation ?? "Unit",
        UnitCost = l.UnitCost,
        Status = EnumDbValue.ToDbValue(l.Status),
        IsOpeningBalance = l.IsOpeningBalance
    };
}