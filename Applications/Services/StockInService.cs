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

public class StockInService : IStockInService
{
    private readonly ScmDbContext _context;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly IPostingTransaction _posting;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<StockInService> _logger;

    public StockInService(
        ScmDbContext context,
        IDocumentNumberService documentNumbers,
        IPostingTransaction posting,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<StockInService> logger)
    {
        _context = context;
        _documentNumbers = documentNumbers;
        _posting = posting;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<List<StockInResponse>>> GetStockInsAsync(string? status = null, int? grnId = null)
    {
        try
        {
            var query = _context.StockIns
                .Include(s => s.GoodsReceipt).ThenInclude(g => g.Supplier)
                .Include(s => s.GoodsReceipt).ThenInclude(g => g.PurchaseOrder)
                .Include(s => s.Lines).ThenInclude(l => l.Item).ThenInclude(i => i.Category)
                .Include(s => s.Lines).ThenInclude(l => l.PurchaseUom)
                .AsQueryable();

            if (grnId.HasValue)
            {
                query = query.Where(s => s.GrnId == grnId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (EnumDbValue.TryParse<StockInStatus>(status, out var parsedStatus))
                    query = query.Where(s => s.Status == parsedStatus);
            }

            var list = await query.OrderByDescending(s => s.StockInId).ToListAsync();
            return ApiResponse<List<StockInResponse>>.SuccessResponse(list.Select(MapToResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Stock-Ins");
            return ApiResponse<List<StockInResponse>>.FailureResponse("An error occurred while fetching Stock-In records.");
        }
    }

    public async Task<ApiResponse<StockInResponse>> GetStockInByIdAsync(int id)
    {
        try
        {
            var stockIn = await _context.StockIns
                .Include(s => s.GoodsReceipt).ThenInclude(g => g.Supplier)
                .Include(s => s.GoodsReceipt).ThenInclude(g => g.PurchaseOrder)
                .Include(s => s.Lines).ThenInclude(l => l.Item).ThenInclude(i => i.Category)
                .Include(s => s.Lines).ThenInclude(l => l.PurchaseUom)
                .FirstOrDefaultAsync(s => s.StockInId == id);

            if (stockIn == null)
                return ApiResponse<StockInResponse>.FailureResponse($"Stock-In record {id} not found.");

            return ApiResponse<StockInResponse>.SuccessResponse(MapToResponse(stockIn));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Stock-In {Id}", id);
            return ApiResponse<StockInResponse>.FailureResponse("An error occurred while fetching Stock-In record.");
        }
    }

    public async Task<ApiResponse<StockInResponse>> CreateStockInAsync(CreateStockInRequest request)
    {
        try
        {
            var grn = await _context.GoodsReceipts
                .Include(g => g.Supplier)
                .Include(g => g.PurchaseOrder).ThenInclude(po => po.PurchaseOrderItems)
                .Include(g => g.Items).ThenInclude(gi => gi.Item)
                .FirstOrDefaultAsync(g => g.GrnId == request.GrnId);

            if (grn == null)
                return ApiResponse<StockInResponse>.FailureResponse($"GRN {request.GrnId} not found.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;
            var stockInNumber = await _documentNumbers.NextAsync(DocumentType.StockIn, now);

            var initialStatus = request.SubmitForApproval ? StockInStatus.PendingApproval : StockInStatus.Draft;

            var stockIn = new StockIn
            {
                StockInNumber = stockInNumber,
                GrnId = grn.GrnId,
                Status = initialStatus,
                CreatedBy = actor.AuditName,
                CreatedAt = now,
                SubmittedBy = request.SubmitForApproval ? actor.AuditName : null,
                SubmittedAt = request.SubmitForApproval ? now : null,
                Notes = request.Notes
            };

            int batchSeq = 1;
            foreach (var lineReq in request.Lines)
            {
                var item = await _context.Items.FindAsync(lineReq.ItemId);
                if (item == null) continue;

                // Current stock before commit across warehouse locations
                var currentStock = await _context.Inventories
                    .Where(inv => inv.ItemId == lineReq.ItemId)
                    .SumAsync(inv => (decimal?)inv.CurrentStock) ?? 0m;

                var lotCode = !string.IsNullOrWhiteSpace(lineReq.LotCode)
                    ? lineReq.LotCode.Trim()
                    : $"LOT-{now:yyyyMMdd}-{item.ItemId}-{batchSeq:D2}";

                var line = new StockInLine
                {
                    StockIn = stockIn,
                    GrnItemId = lineReq.GrnItemId,
                    ItemId = lineReq.ItemId,
                    PurchaseUomId = lineReq.PurchaseUomId ?? item.UomId,
                    QuantityToStock = lineReq.QuantityToStock,
                    CurrentStockBeforeCommit = currentStock,
                    LotCode = lotCode,
                    ExpiryDate = lineReq.ExpiryDate,
                    CommittedToInventory = false,
                    Notes = lineReq.Notes
                };

                stockIn.Lines.Add(line);
                batchSeq++;
            }

            await _posting.ExecuteAsync(async () =>
            {
                _context.StockIns.Add(stockIn);
                _audit.Record(nameof(StockIn), stockIn.StockInNumber, "Created", "Status", null, initialStatus.ToString());
                return stockIn;
            });

            var reloaded = await ReloadStockInAsync(stockIn.StockInId);
            return ApiResponse<StockInResponse>.SuccessResponse(
                MapToResponse(reloaded),
                $"Stock-In {stockIn.StockInNumber} created successfully as {initialStatus}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Stock-In for GRN {GrnId}", request.GrnId);
            return ApiResponse<StockInResponse>.FailureResponse($"Failed to create Stock-In: {ex.Message}");
        }
    }

    public async Task<ApiResponse<StockInResponse>> SubmitForApprovalAsync(int id)
    {
        try
        {
            var stockIn = await _context.StockIns.FindAsync(id);
            if (stockIn == null)
                return ApiResponse<StockInResponse>.FailureResponse($"Stock-In {id} not found.");

            if (stockIn.Status != StockInStatus.Draft)
                return ApiResponse<StockInResponse>.FailureResponse($"Stock-In {stockIn.StockInNumber} is already {stockIn.Status} and cannot be submitted.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            stockIn.Status = StockInStatus.PendingApproval;
            stockIn.SubmittedBy = actor.AuditName;
            stockIn.SubmittedAt = now;

            await _context.SaveChangesAsync();
            _audit.Record(nameof(StockIn), stockIn.StockInNumber, "Submitted", "Status", "Draft", "PendingApproval");

            var reloaded = await ReloadStockInAsync(id);
            return ApiResponse<StockInResponse>.SuccessResponse(MapToResponse(reloaded), $"Stock-In {stockIn.StockInNumber} submitted for admin approval.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting Stock-In {Id}", id);
            return ApiResponse<StockInResponse>.FailureResponse($"Failed to submit Stock-In: {ex.Message}");
        }
    }

    public async Task<ApiResponse<StockInResponse>> ApproveStockInAsync(int id, ApproveStockInRequest request)
    {
        try
        {
            var stockIn = await _context.StockIns
                .Include(s => s.GoodsReceipt).ThenInclude(g => g.PurchaseOrder).ThenInclude(po => po.PurchaseOrderItems)
                .Include(s => s.GoodsReceipt).ThenInclude(g => g.Supplier)
                .Include(s => s.Lines).ThenInclude(l => l.Item)
                .FirstOrDefaultAsync(s => s.StockInId == id);

            if (stockIn == null)
                return ApiResponse<StockInResponse>.FailureResponse($"Stock-In {id} not found.");

            if (stockIn.Status != StockInStatus.PendingApproval && stockIn.Status != StockInStatus.Draft)
                return ApiResponse<StockInResponse>.FailureResponse($"Stock-In {stockIn.StockInNumber} is {stockIn.Status} and cannot be approved.");

            var warehouseLoc = await _context.Locations.FirstOrDefaultAsync(l => l.LocationType == LocationType.Warehouse)
                ?? await _context.Locations.FirstOrDefaultAsync();

            if (warehouseLoc == null)
                return ApiResponse<StockInResponse>.FailureResponse("No warehouse location configured for inventory stock-in.");

            var now = DateTime.UtcNow;
            var actor = _currentUser.Current;
            var approver = !string.IsNullOrWhiteSpace(request.ApproverName) ? request.ApproverName.Trim() : actor.AuditName;

            await _posting.ExecuteAsync(async () =>
            {
                stockIn.Status = StockInStatus.Approved;
                stockIn.ApprovedBy = approver;
                stockIn.ApprovedAt = now;
                if (!string.IsNullOrWhiteSpace(request.Notes))
                {
                    stockIn.Notes = string.IsNullOrWhiteSpace(stockIn.Notes)
                        ? $"Approval notes: {request.Notes}"
                        : $"{stockIn.Notes} | Approval notes: {request.Notes}";
                }

                foreach (var line in stockIn.Lines)
                {
                    if (line.CommittedToInventory) continue;

                    var poItem = stockIn.GoodsReceipt.PurchaseOrder?.PurchaseOrderItems.FirstOrDefault(p => p.ItemId == line.ItemId);

                    var lot = new InventoryLot
                    {
                        LotCode = line.LotCode,
                        ItemId = line.ItemId,
                        LocationId = warehouseLoc.LocationId,
                        SourceType = LotSourceType.Purchased,
                        SupplierId = stockIn.GoodsReceipt.SupplierId,
                        GrnLineId = line.GrnItemId,
                        ReceivedDate = stockIn.GoodsReceipt.ReceivedDate,
                        ExpiryDate = line.ExpiryDate.HasValue ? DateOnly.FromDateTime(line.ExpiryDate.Value) : null,
                        QuantityReceived = line.QuantityToStock,
                        QuantityRemaining = line.QuantityToStock,
                        UomId = line.PurchaseUomId ?? line.Item.UomId,
                        UnitCost = poItem?.UnitPrice ?? 0m,
                        Status = LotStatus.Available
                    };

                    _context.InventoryLots.Add(lot);
                    await _context.SaveChangesAsync();

                    line.InventoryLotId = lot.LotId;
                    line.CommittedToInventory = true;

                    // Update Inventory cache
                    await AdjustInventoryCacheAsync(line.ItemId, warehouseLoc.LocationId, line.QuantityToStock);

                    // Record Stock Ledger
                    var ledger = new StockLedger
                    {
                        LotId = lot.LotId,
                        ItemId = line.ItemId,
                        LocationId = warehouseLoc.LocationId,
                        MovementType = MovementType.PurchaseReceipt,
                        Quantity = line.QuantityToStock,
                        UomId = lot.UomId,
                        UnitCost = lot.UnitCost,
                        PostedAt = now,
                        ReferenceType = "StockIn",
                        ReferenceId = stockIn.StockInNumber,
                        UserId = actor.UserId,
                        UserName = approver,
                        Notes = $"Stock-In approved by {approver} via {stockIn.StockInNumber}"
                    };
                    _context.StockLedgers.Add(ledger);
                }

                // Update GRN status
                var grn = stockIn.GoodsReceipt;
                if (grn != null)
                {
                    grn.Status = GoodsReceiptStatus.FullyPutAway;
                    _context.GoodsReceipts.Update(grn);

                    if (grn.PurchaseOrder != null)
                    {
                        var po = grn.PurchaseOrder;
                        var allFulfilled = po.PurchaseOrderItems.All(poi => poi.ReceivedQuantity >= poi.PoItemQuantity);
                        if (allFulfilled)
                        {
                            po.Status = PurchaseOrderStatus.Completed;
                            _context.PurchaseOrders.Update(po);
                        }
                    }
                }

                _context.StockIns.Update(stockIn);
                _audit.Record(nameof(StockIn), stockIn.StockInNumber, "Approved", "Status", "PendingApproval", "Approved");
                return stockIn;
            });

            var reloaded = await ReloadStockInAsync(id);
            return ApiResponse<StockInResponse>.SuccessResponse(MapToResponse(reloaded), $"Stock-In {stockIn.StockInNumber} approved and committed to inventory.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving Stock-In {Id}", id);
            return ApiResponse<StockInResponse>.FailureResponse($"Failed to approve Stock-In: {ex.Message}");
        }
    }

    public async Task<ApiResponse<StockInResponse>> RejectStockInAsync(int id, RejectStockInRequest request)
    {
        try
        {
            var stockIn = await _context.StockIns.FindAsync(id);
            if (stockIn == null)
                return ApiResponse<StockInResponse>.FailureResponse($"Stock-In {id} not found.");

            if (stockIn.Status == StockInStatus.Approved)
                return ApiResponse<StockInResponse>.FailureResponse($"Stock-In {stockIn.StockInNumber} is already approved and cannot be rejected.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            stockIn.Status = StockInStatus.Rejected;
            stockIn.RejectedBy = !string.IsNullOrWhiteSpace(request.RejectedBy) ? request.RejectedBy.Trim() : actor.AuditName;
            stockIn.RejectedAt = now;
            stockIn.RejectionReason = request.RejectionReason.Trim();
            if (!string.IsNullOrWhiteSpace(request.Notes))
                stockIn.Notes = string.IsNullOrWhiteSpace(stockIn.Notes) ? request.Notes : $"{stockIn.Notes} | {request.Notes}";

            await _context.SaveChangesAsync();
            _audit.Record(nameof(StockIn), stockIn.StockInNumber, "Rejected", "Status", stockIn.Status.ToString(), "Rejected");

            var reloaded = await ReloadStockInAsync(id);
            return ApiResponse<StockInResponse>.SuccessResponse(MapToResponse(reloaded), $"Stock-In {stockIn.StockInNumber} rejected.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting Stock-In {Id}", id);
            return ApiResponse<StockInResponse>.FailureResponse($"Failed to reject Stock-In: {ex.Message}");
        }
    }

    private async Task AdjustInventoryCacheAsync(int itemId, int locationId, decimal deltaQuantity)
    {
        var inv = await _context.Inventories
            .FirstOrDefaultAsync(i => i.ItemId == itemId && i.LocationId == locationId);

        if (inv != null)
        {
            inv.CurrentStock += deltaQuantity;
            _context.Inventories.Update(inv);
        }
        else
        {
            _context.Inventories.Add(new Inventory
            {
                ItemId = itemId,
                LocationId = locationId,
                CurrentStock = deltaQuantity
            });
        }
    }

    private async Task<StockIn> ReloadStockInAsync(int id)
    {
        return await _context.StockIns
            .Include(s => s.GoodsReceipt).ThenInclude(g => g.Supplier)
            .Include(s => s.GoodsReceipt).ThenInclude(g => g.PurchaseOrder)
            .Include(s => s.Lines).ThenInclude(l => l.Item).ThenInclude(i => i.Category)
            .Include(s => s.Lines).ThenInclude(l => l.PurchaseUom)
            .FirstAsync(s => s.StockInId == id);
    }

    private static StockInResponse MapToResponse(StockIn s)
    {
        return new StockInResponse
        {
            StockInId = s.StockInId,
            StockInNumber = s.StockInNumber,
            GrnId = s.GrnId,
            GrnNumber = s.GoodsReceipt?.GrnNumber ?? string.Empty,
            SupplierId = s.GoodsReceipt?.SupplierId ?? 0,
            SupplierName = s.GoodsReceipt?.Supplier?.CompanyName ?? string.Empty,
            PoNumber = s.GoodsReceipt?.PurchaseOrder?.PoNumber ?? string.Empty,
            Status = s.Status.ToString(),
            CreatedBy = s.CreatedBy,
            CreatedAt = s.CreatedAt,
            SubmittedBy = s.SubmittedBy,
            SubmittedAt = s.SubmittedAt,
            ApprovedBy = s.ApprovedBy,
            ApprovedAt = s.ApprovedAt,
            RejectedBy = s.RejectedBy,
            RejectedAt = s.RejectedAt,
            RejectionReason = s.RejectionReason,
            Notes = s.Notes,
            Lines = s.Lines.Select(l => new StockInLineResponse
            {
                StockInLineId = l.StockInLineId,
                StockInId = l.StockInId,
                GrnItemId = l.GrnItemId,
                ItemId = l.ItemId,
                ItemName = l.Item?.ItemName ?? $"Item #{l.ItemId}",
                ItemCode = l.Item?.ItemCode ?? string.Empty,
                CategoryName = l.Item?.Category?.CategoryName ?? "Raw Materials",
                PurchaseUomId = l.PurchaseUomId,
                PurchaseUomName = l.PurchaseUom?.Abbreviation ?? l.PurchaseUom?.Name ?? "Unit",
                QuantityToStock = l.QuantityToStock,
                CurrentStockBeforeCommit = l.CurrentStockBeforeCommit,
                LotCode = l.LotCode,
                ExpiryDate = l.ExpiryDate,
                CommittedToInventory = l.CommittedToInventory,
                InventoryLotId = l.InventoryLotId,
                Notes = l.Notes
            }).ToList()
        };
    }
}
