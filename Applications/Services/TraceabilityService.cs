using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class TraceabilityService : ITraceabilityService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<TraceabilityService> _logger;

    public TraceabilityService(ScmDbContext context, ILogger<TraceabilityService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<ForwardTraceResponse>> TraceForwardAsync(string lotCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(lotCode))
                return ApiResponse<ForwardTraceResponse>.FailureResponse("Lot code is required.");

            var lot = await _context.InventoryLots
                .Include(l => l.Item)
                .Include(l => l.Supplier)
                .FirstOrDefaultAsync(l => l.LotCode == lotCode.Trim());

            if (lot == null)
                return ApiResponse<ForwardTraceResponse>.FailureResponse($"Lot '{lotCode}' was not found.");

            var grnItem = await _context.GoodsReceiptItems
                .Include(g => g.GoodsReceipt)
                .Include(g => g.PurchaseOrderItem).ThenInclude(poi => poi.PurchaseOrder)
                .FirstOrDefaultAsync(g => g.LotId == lot.LotId || (lot.GrnLineId.HasValue && g.GrnItemId == lot.GrnLineId.Value));

            var po = grnItem?.PurchaseOrderItem?.PurchaseOrder;

            // 1. Find all Production Batches where this lot was consumed
            var consumptions = await _context.BatchConsumptions
                .Include(c => c.Batch).ThenInclude(b => b!.Product).ThenInclude(p => p!.Item)
                .Where(c => c.LotId == lot.LotId)
                .ToListAsync();

            var batchIds = consumptions.Select(c => c.BatchId).Distinct().ToList();

            var batches = await _context.ProductionBatches
                .Include(b => b.Product).ThenInclude(p => p!.Item)
                .Include(b => b.FgLot)
                .Where(b => batchIds.Contains(b.BatchId))
                .ToListAsync();

            // 2. Find Finished Goods Lots produced from these batches
            var fgLotIds = batches.Where(b => b.FgLotId.HasValue).Select(b => b.FgLotId!.Value).ToList();
            var fgLots = await _context.InventoryLots
                .Include(l => l.Item)
                .Where(l => fgLotIds.Contains(l.LotId) || (l.ProductionOrderId.HasValue && batchIds.Contains(l.ProductionOrderId.Value)))
                .ToListAsync();

            // 3. Find Downstream Branch Shipments carrying these finished products
            var productIds = batches.Select(b => b.ProductId).Distinct().ToList();
            var shipments = await _context.StockTransfers
                .Include(st => st.Product).ThenInclude(p => p!.Item)
                .Include(st => st.DestLocation)
                .Where(st => productIds.Contains(st.ProductId))
                .ToListAsync();

            if (batches.Any(b => b.CompletedDate.HasValue))
            {
                var minCompletedDate = batches.Where(b => b.CompletedDate.HasValue).Min(b => b.CompletedDate!.Value);
                shipments = shipments.Where(st => st.TransferDate >= minCompletedDate).ToList();
            }

            var response = new ForwardTraceResponse
            {
                SourceLot = new LotSummary
                {
                    LotId = lot.LotId,
                    LotCode = lot.LotCode,
                    ItemId = lot.ItemId,
                    ItemName = lot.Item?.ItemName ?? $"Item {lot.ItemId}",
                    SourceType = EnumDbValue.ToDbValue(lot.SourceType),
                    QuantityReceived = lot.QuantityReceived,
                    QuantityRemaining = lot.QuantityRemaining,
                    Status = EnumDbValue.ToDbValue(lot.Status),
                    ManufactureDate = lot.ManufactureDate,
                    ExpiryDate = lot.ExpiryDate,
                    ReceivedDate = lot.ReceivedDate,
                    UnitCost = lot.UnitCost
                },
                SupplierOrigin = lot.Supplier != null ? new SupplierTraceSummary
                {
                    SupplierId = lot.Supplier.SupplierId,
                    SupplierName = lot.Supplier.CompanyName,
                    PoNumber = po?.PoNumber,
                    PoOrderDate = po?.OrderDate
                } : null,
                GoodsReceiptOrigin = grnItem?.GoodsReceipt != null ? new GrnTraceSummary
                {
                    GrnId = grnItem.GoodsReceipt.GrnId,
                    GrnNumber = grnItem.GoodsReceipt.GrnNumber,
                    ReceivedDate = grnItem.GoodsReceipt.ReceivedDate,
                    DeliveryNoteNumber = grnItem.GoodsReceipt.DeliveryNoteNumber
                } : null,
                AffectedProductionBatches = batches.Select(b => new BatchTraceSummary
                {
                    BatchId = b.BatchId,
                    BatchNumber = b.BatchNumber,
                    ProductId = b.ProductId,
                    ProductName = b.Product?.Item?.ItemName ?? $"Product {b.ProductId}",
                    ActualQuantity = b.ActualQuantity,
                    ProductionDate = b.ProductionDate,
                    Status = EnumDbValue.ToDbValue(b.Status),
                    QualityStatus = EnumDbValue.ToDbValue(b.QualityStatus),
                    AssignedCook = b.AssignedCook
                }).ToList(),
                AffectedFinishedGoodsLots = fgLots.Select(fg => new FinishedGoodsTraceSummary
                {
                    LotId = fg.LotId,
                    LotCode = fg.LotCode,
                    ProductName = fg.Item?.ItemName ?? $"Item {fg.ItemId}",
                    QuantityRemaining = fg.QuantityRemaining,
                    Status = EnumDbValue.ToDbValue(fg.Status),
                    ExpiryDate = fg.ExpiryDate
                }).ToList(),
                DownstreamBranchShipments = shipments.Select(st => new ShipmentTraceSummary
                {
                    TransferId = st.TransferId,
                    TransferNumber = st.TransferNumber,
                    DestinationBranchId = st.DestLocationId,
                    DestinationBranchName = st.DestLocation?.LocationName ?? $"Location {st.DestLocationId}",
                    QuantityShipped = st.TransferQuantity,
                    Status = EnumDbValue.ToDbValue(st.Status),
                    DriverName = st.DriverName,
                    DispatchedDate = st.DispatchedDate,
                    ReceivedDate = st.ReceivedDate
                }).ToList()
            };

            return ApiResponse<ForwardTraceResponse>.SuccessResponse(
                response,
                $"Forward trace completed for lot {lotCode}. Traced {batches.Count} batches, {fgLots.Count} FG lots, and {shipments.Count} branch shipments.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing forward trace for lot {LotCode}", lotCode);
            return ApiResponse<ForwardTraceResponse>.FailureResponse($"Forward trace failed: {ex.Message}");
        }
    }

    public async Task<ApiResponse<BackwardTraceResponse>> TraceBackwardAsync(string fgLotCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fgLotCode))
                return ApiResponse<BackwardTraceResponse>.FailureResponse("Finished goods lot code is required.");

            var fgLot = await _context.InventoryLots
                .Include(l => l.Item)
                .FirstOrDefaultAsync(l => l.LotCode == fgLotCode.Trim());

            if (fgLot == null)
                return ApiResponse<BackwardTraceResponse>.FailureResponse($"Finished goods lot '{fgLotCode}' not found.");

            // Find manufacturing production batch
            ProductionBatch? batch = null;
            if (fgLot.ProductionOrderId.HasValue)
            {
                batch = await _context.ProductionBatches
                    .Include(b => b.Product).ThenInclude(p => p!.Item)
                    .FirstOrDefaultAsync(b => b.BatchId == fgLot.ProductionOrderId.Value);
            }
            else
            {
                batch = await _context.ProductionBatches
                    .Include(b => b.Product).ThenInclude(p => p!.Item)
                    .FirstOrDefaultAsync(b => b.FgLotId == fgLot.LotId);
            }

            var consumedItems = new List<IngredientLotTraceSummary>();

            if (batch != null)
            {
                var consumptions = await _context.BatchConsumptions
                    .Include(c => c.Item)
                    .Include(c => c.Lot).ThenInclude(l => l!.Supplier)
                    .Where(c => c.BatchId == batch.BatchId)
                    .ToListAsync();

                foreach (var c in consumptions)
                {
                    consumedItems.Add(new IngredientLotTraceSummary
                    {
                        ItemId = c.ItemId,
                        ItemName = c.Item?.ItemName ?? $"Item {c.ItemId}",
                        LotId = c.LotId,
                        LotCode = c.Lot?.LotCode ?? "N/A",
                        QuantityUsed = c.QuantityUsed,
                        UnitCost = c.UnitCost,
                        SupplierId = c.Lot?.SupplierId,
                        SupplierName = c.Lot?.Supplier?.CompanyName ?? "Internal/Unknown",
                        ExpiryDate = c.Lot?.ExpiryDate
                    });
                }
            }

            var response = new BackwardTraceResponse
            {
                FinishedGoodsLot = new LotSummary
                {
                    LotId = fgLot.LotId,
                    LotCode = fgLot.LotCode,
                    ItemId = fgLot.ItemId,
                    ItemName = fgLot.Item?.ItemName ?? $"Item {fgLot.ItemId}",
                    SourceType = EnumDbValue.ToDbValue(fgLot.SourceType),
                    QuantityReceived = fgLot.QuantityReceived,
                    QuantityRemaining = fgLot.QuantityRemaining,
                    Status = EnumDbValue.ToDbValue(fgLot.Status),
                    ManufactureDate = fgLot.ManufactureDate,
                    ExpiryDate = fgLot.ExpiryDate,
                    ReceivedDate = fgLot.ReceivedDate,
                    UnitCost = fgLot.UnitCost
                },
                ProductionBatch = batch != null ? new BatchTraceSummary
                {
                    BatchId = batch.BatchId,
                    BatchNumber = batch.BatchNumber,
                    ProductId = batch.ProductId,
                    ProductName = batch.Product?.Item?.ItemName ?? $"Product {batch.ProductId}",
                    ActualQuantity = batch.ActualQuantity,
                    ProductionDate = batch.ProductionDate,
                    Status = EnumDbValue.ToDbValue(batch.Status),
                    QualityStatus = EnumDbValue.ToDbValue(batch.QualityStatus),
                    AssignedCook = batch.AssignedCook
                } : null,
                ConsumedIngredientsAndPackaging = consumedItems
            };

            return ApiResponse<BackwardTraceResponse>.SuccessResponse(
                response,
                $"Backward trace completed for {fgLotCode}. Traced {consumedItems.Count} consumed ingredients and packaging lots.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing backward trace for {LotCode}", fgLotCode);
            return ApiResponse<BackwardTraceResponse>.FailureResponse($"Backward trace failed: {ex.Message}");
        }
    }
}
