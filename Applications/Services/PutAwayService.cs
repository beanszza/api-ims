using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class PutAwayService : IPutAwayService
{
    private readonly ScmDbContext _context;
    private readonly IPostingTransaction _posting;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<PutAwayService> _logger;

    public PutAwayService(
        ScmDbContext context,
        IPostingTransaction posting,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<PutAwayService> logger)
    {
        _context = context;
        _posting = posting;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<List<PutAwayResponse>>> GetPutAwaysAsync(string? status = null, int? grnId = null)
    {
        try
        {
            var query = _context.PutAwayTransactions
                .Include(p => p.GoodsReceipt).ThenInclude(g => g.PurchaseOrder).ThenInclude(po => po.PurchaseRequisition)
                .Include(p => p.GoodsReceipt).ThenInclude(g => g.Supplier)
                .Include(p => p.Item).ThenInclude(i => i.Uom)
                .Include(p => p.Uom)
                .Include(p => p.DestinationLocation)
                .Include(p => p.Lot)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (EnumDbValue.TryParse<PutAwayStatus>(status, out var ps))
                    query = query.Where(p => p.Status == ps);
            }

            if (grnId.HasValue)
                query = query.Where(p => p.GrnId == grnId.Value);

            var list = await query.OrderByDescending(p => p.PutAwayId).ToListAsync();
            return ApiResponse<List<PutAwayResponse>>.SuccessResponse(list.Select(MapToResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Put Away tasks");
            return ApiResponse<List<PutAwayResponse>>.FailureResponse("An error occurred while fetching Put Away tasks.");
        }
    }

    public async Task<ApiResponse<PutAwayResponse>> GetPutAwayByIdAsync(int id)
    {
        try
        {
            var pa = await _context.PutAwayTransactions
                .Include(p => p.GoodsReceipt).ThenInclude(g => g.PurchaseOrder).ThenInclude(po => po.PurchaseRequisition)
                .Include(p => p.GoodsReceipt).ThenInclude(g => g.Supplier)
                .Include(p => p.Item).ThenInclude(i => i.Uom)
                .Include(p => p.Uom)
                .Include(p => p.DestinationLocation)
                .Include(p => p.Lot)
                .FirstOrDefaultAsync(p => p.PutAwayId == id);

            if (pa == null)
                return ApiResponse<PutAwayResponse>.FailureResponse($"Put Away task {id} not found.");

            return ApiResponse<PutAwayResponse>.SuccessResponse(MapToResponse(pa));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Put Away task {Id}", id);
            return ApiResponse<PutAwayResponse>.FailureResponse("An error occurred while fetching Put Away task.");
        }
    }

    public async Task<ApiResponse<PutAwayResponse>> CompletePutAwayAsync(int id, CompletePutAwayRequest request)
    {
        try
        {
            var pa = await _context.PutAwayTransactions
                .Include(p => p.GoodsReceipt).ThenInclude(g => g.PurchaseOrder).ThenInclude(po => po.PurchaseRequisition)
                .Include(p => p.GoodsReceipt).ThenInclude(g => g.Supplier)
                .Include(p => p.Item)
                .Include(p => p.Lot)
                .FirstOrDefaultAsync(p => p.PutAwayId == id);

            if (pa == null)
                return ApiResponse<PutAwayResponse>.FailureResponse($"Put Away task {id} not found.");

            if (pa.Status != PutAwayStatus.Pending)
                return ApiResponse<PutAwayResponse>.FailureResponse($"Put Away task {pa.PutAwayNumber} is already {pa.Status}.");

            var locId = request.DestinationLocationId > 0
                ? request.DestinationLocationId
                : 5;
            var location = await _context.Locations.FindAsync(locId)
                ?? await _context.Locations.FirstOrDefaultAsync(l => l.LocationType == Domains.Enums.LocationType.Warehouse);

            if (location == null)
                return ApiResponse<PutAwayResponse>.FailureResponse($"Destination warehouse location not found.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            await _posting.ExecuteAsync(async () =>
            {
                pa.DestinationLocationId = location.LocationId;
                pa.DestinationLocation = location;
                pa.Status = PutAwayStatus.Completed;
                pa.CompletedAt = now;
                pa.PerformedBy = actor.AuditName;
                if (!string.IsNullOrWhiteSpace(request.LotCode))
                    pa.LotCode = request.LotCode.Trim();
                if (request.ExpiryDate.HasValue)
                    pa.ExpiryDate = request.ExpiryDate.Value;
                if (!string.IsNullOrWhiteSpace(request.SerialNumber))
                    pa.SerialNumber = request.SerialNumber.Trim();
                if (!string.IsNullOrWhiteSpace(request.Notes))
                    pa.Notes = request.Notes;

                // Accepted goods first become inventory here. GRN and QA never create usable stock.
                if (pa.Lot == null)
                {
                    var grnItem = await _context.GoodsReceiptItems.FindAsync(pa.GrnItemId)
                        ?? throw new InvalidOperationException("The source GRN item no longer exists.");
                    var poItem = pa.GoodsReceipt.PurchaseOrder?.PurchaseOrderItems.FirstOrDefault(i => i.PoItemId == grnItem.PoItemId);
                    var lotCode = !string.IsNullOrWhiteSpace(request.LotCode)
                        ? request.LotCode.Trim()
                        : !string.IsNullOrWhiteSpace(pa.LotCode) ? pa.LotCode : $"LOT-{pa.PutAwayNumber}";

                    pa.Lot = new InventoryLot
                    {
                        LotCode = lotCode,
                        ItemId = pa.ItemId,
                        LocationId = location.LocationId,
                        SourceType = LotSourceType.Purchased,
                        SupplierId = pa.GoodsReceipt.SupplierId,
                        GrnLineId = grnItem.GrnItemId,
                        SupplierLotNo = grnItem.SupplierLotCode,
                        ReceivedDate = now,
                        ManufactureDate = grnItem.ManufactureDate,
                        ExpiryDate = request.ExpiryDate ?? grnItem.ExpiryDate,
                        QuantityReceived = pa.AcceptedQuantity,
                        QuantityRemaining = pa.AcceptedQuantity,
                        UomId = pa.UomId,
                        UnitCost = poItem?.UnitPrice ?? 0m,
                        Status = LotStatus.Available
                    };
                    _context.InventoryLots.Add(pa.Lot);
                }

                if (pa.Lot != null)
                {
                    pa.Lot.LocationId = location.LocationId;
                    pa.Lot.Status = LotStatus.Available;
                    if (!string.IsNullOrWhiteSpace(request.LotCode))
                        pa.Lot.LotCode = request.LotCode.Trim();
                    if (request.ExpiryDate.HasValue)
                        pa.Lot.ExpiryDate = request.ExpiryDate.Value;

                    _context.InventoryLots.Update(pa.Lot);

                    // Update Inventory balance cache
                    await AdjustInventoryCacheAsync(pa.Lot.ItemId, location.LocationId, pa.AcceptedQuantity);

                    // Write to StockLedger
                    var ledger = new StockLedger
                    {
                        LotId = pa.Lot.LotId,
                        ItemId = pa.Lot.ItemId,
                        LocationId = location.LocationId,
                        MovementType = MovementType.PurchaseReceipt,
                        Quantity = pa.AcceptedQuantity,
                        UomId = pa.Lot.UomId,
                        UnitCost = pa.Lot.UnitCost,
                        PostedAt = now,
                        ReferenceType = "PutAway",
                        ReferenceId = pa.PutAwayNumber,
                        UserId = actor.UserId,
                        UserName = actor.AuditName,
                        Notes = $"Put away completed to {location.LocationName} via {pa.PutAwayNumber}"
                    };
                    _context.StockLedgers.Add(ledger);
                }

                _context.PutAwayTransactions.Update(pa);

                // Update GRN PutAway status
                var grnId = pa.GrnId;
                var allTasksForGrn = await _context.PutAwayTransactions
                    .Where(p => p.GrnId == grnId)
                    .ToListAsync();

                var allCompleted = allTasksForGrn.All(p => p.PutAwayId == pa.PutAwayId || p.Status == PutAwayStatus.Completed);
                var grn = pa.GoodsReceipt;
                if (grn != null)
                {
                    grn.Status = allCompleted ? GoodsReceiptStatus.FullyPutAway : GoodsReceiptStatus.PartiallyPutAway;
                    _context.GoodsReceipts.Update(grn);

                    // Check Purchase Order status
                    if (allCompleted && grn.PurchaseOrder != null)
                    {
                        var po = grn.PurchaseOrder;
                        // If all items in PO have been fulfilled
                        var allPoItemsDelivered = po.PurchaseOrderItems.All(poi => poi.ReceivedQuantity >= poi.PoItemQuantity);
                        if (allPoItemsDelivered)
                        {
                            po.Status = PurchaseOrderStatus.Completed;
                            _context.PurchaseOrders.Update(po);
                        }
                    }
                }

                _audit.Record(nameof(PutAwayTransaction), pa.PutAwayNumber, "Completed", "Status", "Pending", "Completed");
                return pa;
            });

            return await GetPutAwayByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing Put Away task {Id}", id);
            return ApiResponse<PutAwayResponse>.FailureResponse($"Failed to complete Put Away: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<PutAwayResponse>>> BatchCompletePutAwayAsync(BatchCompletePutAwayRequest request)
    {
        try
        {
            if (request.Items == null || !request.Items.Any())
                return ApiResponse<List<PutAwayResponse>>.FailureResponse("No Put Away items supplied.");

            var results = new List<PutAwayResponse>();
            foreach (var item in request.Items)
            {
                var completeReq = new CompletePutAwayRequest
                {
                    DestinationLocationId = item.DestinationLocationId,
                    LotCode = item.LotCode,
                    ExpiryDate = item.ExpiryDate,
                    SerialNumber = item.SerialNumber,
                    Notes = item.Notes
                };

                var res = await CompletePutAwayAsync(item.PutAwayId, completeReq);
                if (!res.Success)
                    return ApiResponse<List<PutAwayResponse>>.FailureResponse(res.Message);

                if (res.Data != null)
                    results.Add(res.Data);
            }

            return ApiResponse<List<PutAwayResponse>>.SuccessResponse(results, "All Put Away tasks successfully completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch completing Put Away tasks");
            return ApiResponse<List<PutAwayResponse>>.FailureResponse($"Failed to batch complete Put Away: {ex.Message}");
        }
    }

    private async Task AdjustInventoryCacheAsync(int itemId, int locationId, decimal delta)
    {
        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.ItemId == itemId && i.LocationId == locationId);

        if (inventory == null)
        {
            inventory = new Inventory
            {
                ItemId = itemId,
                LocationId = locationId,
                CurrentStock = delta
            };
            _context.Inventories.Add(inventory);
        }
        else
        {
            inventory.CurrentStock += delta;
            _context.Inventories.Update(inventory);
        }
    }

    private static PutAwayResponse MapToResponse(PutAwayTransaction p) => new()
    {
        PutAwayId = p.PutAwayId,
        PutAwayNumber = p.PutAwayNumber,
        GrnId = p.GrnId,
        GrnNumber = p.GoodsReceipt?.GrnNumber ?? string.Empty,
        PoId = p.GoodsReceipt?.PoId,
        PoNumber = p.GoodsReceipt?.PurchaseOrder?.PoNumber,
        PrId = p.GoodsReceipt?.PurchaseOrder?.PrId,
        PrNumber = p.GoodsReceipt?.PurchaseOrder?.PurchaseRequisition?.PrNumber,
        SupplierName = p.GoodsReceipt?.Supplier?.CompanyName,
        GrnItemId = p.GrnItemId,
        QaInspectionId = p.QaInspectionId,
        ItemId = p.ItemId,
        ItemName = p.Item?.ItemName ?? $"Item {p.ItemId}",
        AcceptedQuantity = p.AcceptedQuantity,
        UomId = p.UomId,
        UomName = p.Uom?.Abbreviation ?? "Unit",
        DestinationLocationId = p.DestinationLocationId,
        DestinationLocationName = p.DestinationLocation?.LocationName,
        LotId = p.LotId,
        LotCode = p.LotCode ?? p.Lot?.LotCode,
        ExpiryDate = p.ExpiryDate ?? p.Lot?.ExpiryDate,
        SerialNumber = p.SerialNumber,
        Status = EnumDbValue.ToDbValue(p.Status),
        PerformedBy = p.PerformedBy,
        CreatedAt = p.CreatedAt,
        CompletedAt = p.CompletedAt,
        Notes = p.Notes,
        IsLotTracked = p.Item?.IsLotTracked ?? true,
        IsExpiryTracked = p.Item?.IsExpiryTracked ?? false
    };
}
