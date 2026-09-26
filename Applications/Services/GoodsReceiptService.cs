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

public class GoodsReceiptService : IGoodsReceiptService
{
    private readonly ScmDbContext _context;
    private readonly IPostingTransaction _posting;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly ILocationResolver _locations;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<GoodsReceiptService> _logger;

    public GoodsReceiptService(
        ScmDbContext context,
        IPostingTransaction posting,
        IDocumentNumberService documentNumbers,
        ILocationResolver locations,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<GoodsReceiptService> logger)
    {
        _context = context;
        _posting = posting;
        _documentNumbers = documentNumbers;
        _locations = locations;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<List<GoodsReceiptResponse>>> GetGoodsReceiptsAsync(int? poId = null, int? deliveryId = null, string? status = null)
    {
        try
        {
            var query = _context.GoodsReceipts
                .Include(g => g.PurchaseOrder).ThenInclude(p => p.PurchaseRequisition)
                .Include(g => g.Delivery)
                .Include(g => g.Supplier)
                .Include(g => g.ReceivingLocation)
                .Include(g => g.Items).ThenInclude(i => i.Item)
                .Include(g => g.Items).ThenInclude(i => i.PurchaseUom)
                .Include(g => g.Items).ThenInclude(i => i.Lot)
                .AsQueryable();

            if (poId.HasValue)
                query = query.Where(g => g.PoId == poId.Value);

            if (deliveryId.HasValue)
                query = query.Where(g => g.DeliveryId == deliveryId.Value);

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (EnumDbValue.TryParse<GoodsReceiptStatus>(status, out var parsedStatus))
                    query = query.Where(g => g.Status == parsedStatus);
            }

            var list = await query.OrderByDescending(g => g.GrnId).ToListAsync();
            return ApiResponse<List<GoodsReceiptResponse>>.SuccessResponse(list.Select(MapToResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Goods Receipt Notes");
            return ApiResponse<List<GoodsReceiptResponse>>.FailureResponse("An error occurred while fetching goods receipt notes.");
        }
    }

    public async Task<ApiResponse<GoodsReceiptResponse>> GetGoodsReceiptByIdAsync(int grnId)
    {
        try
        {
            var grn = await _context.GoodsReceipts
                .Include(g => g.PurchaseOrder)
                .Include(g => g.Delivery)
                .Include(g => g.Supplier)
                .Include(g => g.ReceivingLocation)
                .Include(g => g.Items).ThenInclude(i => i.Item)
                .Include(g => g.Items).ThenInclude(i => i.PurchaseUom)
                .Include(g => g.Items).ThenInclude(i => i.Lot)
                .FirstOrDefaultAsync(g => g.GrnId == grnId);

            if (grn == null)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Goods receipt note {grnId} not found.");

            return ApiResponse<GoodsReceiptResponse>.SuccessResponse(MapToResponse(grn));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving GRN {GrnId}", grnId);
            return ApiResponse<GoodsReceiptResponse>.FailureResponse("An error occurred while fetching goods receipt note.");
        }
    }

    public async Task<ApiResponse<GoodsReceiptResponse>> CreateDraftGrnAsync(CreateGoodsReceiptRequest request)
    {
        try
        {
            var delivery = await _context.Deliveries
                .Include(d => d.Items)
                .Include(d => d.PurchaseOrder).ThenInclude(p => p.PurchaseOrderItems).ThenInclude(poi => poi.Item)
                .Include(d => d.PurchaseOrder).ThenInclude(p => p.Supplier)
                .FirstOrDefaultAsync(d => d.DeliveryId == request.DeliveryId);

            if (delivery == null)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Delivery {request.DeliveryId} not found.");
            if (delivery.Status != DeliveryStatus.Arrived)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse("A GRN can only be created for a delivery that has arrived at the Commissary.");

            var po = delivery.PurchaseOrder;
            if (po.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.Ordered or PurchaseOrderStatus.Arrived or PurchaseOrderStatus.Completed))
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Cannot receive goods against a {po.Status} purchase order.");

            if (request.Items == null || !request.Items.Any())
                return ApiResponse<GoodsReceiptResponse>.FailureResponse("At least one line item must be received.");

            var actor = _currentUser.Current;
            var receiveDate = DateTime.UtcNow;
            var receivingLocation = await _locations.ResolveReceivingLocationAsync(null);
            var receivingLocationId = receivingLocation.LocationId;

            var grnNumber = await _documentNumbers.NextAsync(DocumentType.GoodsReceipt, receiveDate);

            // Check delivery item reference
            if (request.Items.Any(i => !i.DeliveryItemId.HasValue))
                return ApiResponse<GoodsReceiptResponse>.FailureResponse("Each GRN line must reference a valid delivery item.");

            var itemBatchTotals = request.Items
                .Where(i => i.DeliveryItemId.HasValue)
                .GroupBy(i => i.DeliveryItemId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.DeliveredQuantity));

            var poItemIds = request.Items.Select(i => i.PoItemId).Distinct().ToList();
            var prevReceivedLookup = await _context.GoodsReceiptItems
                .Where(gri => poItemIds.Contains(gri.PoItemId) 
                           && gri.GoodsReceipt.Status != GoodsReceiptStatus.Draft 
                           && gri.GoodsReceipt.Status != GoodsReceiptStatus.Cancelled)
                .GroupBy(gri => gri.PoItemId)
                .Select(g => new { PoItemId = g.Key, TotalReceived = g.Sum(x => x.DeliveredQuantity) })
                .ToDictionaryAsync(x => x.PoItemId, x => x.TotalReceived);

            var grn = new GoodsReceipt
            {
                GrnNumber = grnNumber,
                PoId = po.PoId,
                DeliveryId = delivery.DeliveryId,
                SupplierId = po.SupplierId,
                ReceivingLocationId = receivingLocationId,
                ReceivedDate = receiveDate,
                DeliveryNoteNumber = string.IsNullOrWhiteSpace(request.DeliveryNoteNumber) ? delivery.DeliveryNoteNumber ?? grnNumber : request.DeliveryNoteNumber.Trim(),
                SupplierDrNumber = request.SupplierDrNumber?.Trim(),
                SupplierInvoiceNumber = request.SupplierInvoiceNumber?.Trim(),
                ReceivingBay = request.ReceivingBay?.Trim(),
                Carrier = request.Carrier,
                ReceivedBy = actor.AuditName,
                CreatedBy = actor.AuditName,
                CreatedAt = receiveDate,
                Status = GoodsReceiptStatus.Draft,
                Notes = request.Notes
            };

            var grnItems = new List<GoodsReceiptItem>();
            foreach (var itemReq in request.Items)
            {
                var deliveryItem = delivery.Items.FirstOrDefault(i => i.DeliveryItemId == itemReq.DeliveryItemId);
                var poItem = po.PurchaseOrderItems.FirstOrDefault(i => i.PoItemId == itemReq.PoItemId);
                if (deliveryItem == null || poItem == null || deliveryItem.PoItemId != poItem.PoItemId || deliveryItem.ItemId != poItem.ItemId)
                    return ApiResponse<GoodsReceiptResponse>.FailureResponse("Every GRN line must match its selected delivery item and PO item.");
                if (itemReq.ItemId.HasValue && itemReq.ItemId.Value != deliveryItem.ItemId)
                    return ApiResponse<GoodsReceiptResponse>.FailureResponse("GRN item does not match the source delivery item.");
                if (itemReq.DeliveredQuantity < 0)
                    return ApiResponse<GoodsReceiptResponse>.FailureResponse("Actual received quantity cannot be negative.");

                var prevRcv = prevReceivedLookup.TryGetValue(itemReq.PoItemId, out var val) ? val : 0;
                var declared = deliveryItem.DeclaredQuantity;
                var delivered = itemReq.DeliveredQuantity;
                var totalDeliveredForDeliveryItem = itemBatchTotals.TryGetValue(deliveryItem.DeliveryItemId, out var batchTotal) ? batchTotal : delivered;
                var variance = totalDeliveredForDeliveryItem - declared;
                var varianceType = variance < 0 ? "Short" : (variance > 0 ? "Over" : "Exact");

                grnItems.Add(new GoodsReceiptItem
                {
                    PoItemId = itemReq.PoItemId,
                    ItemId = poItem.ItemId,
                    OrderedQuantity = poItem.PoItemQuantity,
                    PreviouslyReceivedQuantity = prevRcv,
                    DeclaredQuantity = declared,
                    DeliveredQuantity = delivered,
                    VarianceQuantity = Math.Abs(variance),
                    VarianceType = varianceType,
                    DeliveryItemId = deliveryItem.DeliveryItemId,
                    PurchaseUomId = poItem.PurchaseUomId,
                    SupplierLotCode = itemReq.SupplierLotCode?.Trim(),
                    ManufactureDate = itemReq.ManufactureDate,
                    ExpiryDate = itemReq.ExpiryDate,
                    Notes = itemReq.Notes
                });
            }

            grn.Items = grnItems;

            await _posting.ExecuteAsync(async () =>
            {
                _context.GoodsReceipts.Add(grn);
                _audit.Record(nameof(GoodsReceipt), grn.GrnNumber, "Created", "Status", null, "Draft");
                return grn;
            });

            var created = await ReloadGrnAsync(grn.GrnId);
            return ApiResponse<GoodsReceiptResponse>.SuccessResponse(MapToResponse(created), $"Draft GRN {grn.GrnNumber} created. Review counts before posting.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating draft GRN for delivery {DeliveryId}", request.DeliveryId);
            return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Failed to create draft GRN: {ex.Message}");
        }
    }

    public async Task<ApiResponse<GoodsReceiptResponse>> UpdateDraftGrnAsync(int grnId, UpdateGoodsReceiptRequest request)
    {
        try
        {
            var grn = await _context.GoodsReceipts
                .Include(g => g.PurchaseOrder).ThenInclude(p => p.PurchaseOrderItems)
                .Include(g => g.Items)
                .FirstOrDefaultAsync(g => g.GrnId == grnId);

            if (grn == null)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"GRN {grnId} not found.");

            if (grn.Status != GoodsReceiptStatus.Draft)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Only draft GRNs can be edited. Current status is {grn.Status}.");

            if (!string.IsNullOrWhiteSpace(request.DeliveryNoteNumber))
                grn.DeliveryNoteNumber = request.DeliveryNoteNumber.Trim();
            if (request.SupplierDrNumber != null)
                grn.SupplierDrNumber = request.SupplierDrNumber.Trim();
            if (request.SupplierInvoiceNumber != null)
                grn.SupplierInvoiceNumber = request.SupplierInvoiceNumber.Trim();
            if (request.ReceivingBay != null)
                grn.ReceivingBay = request.ReceivingBay.Trim();
            if (request.Carrier != null)
                grn.Carrier = request.Carrier.Trim();
            if (request.Notes != null)
                grn.Notes = request.Notes;

            if (request.Items != null && request.Items.Any())
            {
                // Re-calculate quantities and update items
                foreach (var itemReq in request.Items)
                {
                    var existingItem = grn.Items.FirstOrDefault(i => i.PoItemId == itemReq.PoItemId);
                    if (existingItem != null)
                    {
                        existingItem.DeliveredQuantity = itemReq.DeliveredQuantity;
                        existingItem.SupplierLotCode = itemReq.SupplierLotCode;
                        existingItem.ManufactureDate = itemReq.ManufactureDate ?? existingItem.ManufactureDate;
                        existingItem.ExpiryDate = itemReq.ExpiryDate ?? existingItem.ExpiryDate;
                        existingItem.Notes = itemReq.Notes;

                        var variance = itemReq.DeliveredQuantity - (existingItem.DeclaredQuantity ?? 0m);
                        existingItem.VarianceQuantity = Math.Abs(variance);
                        existingItem.VarianceType = variance < 0 ? "Short" : (variance > 0 ? "Over" : "None");
                    }
                }
            }

            await _context.SaveChangesAsync();

            var updated = await ReloadGrnAsync(grn.GrnId);
            return ApiResponse<GoodsReceiptResponse>.SuccessResponse(MapToResponse(updated), $"Goods Receipt Note {grn.GrnNumber} updated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating draft GRN {GrnId}", grnId);
            return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Failed to update draft GRN: {ex.Message}");
        }
    }

    public async Task<ApiResponse<GoodsReceiptResponse>> PostGrnAsync(int grnId)
    {
        try
        {
            var grn = await _context.GoodsReceipts
                .Include(g => g.PurchaseOrder).ThenInclude(p => p.PurchaseOrderItems)
                .Include(g => g.Delivery)
                .Include(g => g.Items).ThenInclude(i => i.Item)
                .FirstOrDefaultAsync(g => g.GrnId == grnId);

            if (grn == null)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"GRN {grnId} not found.");

            if (grn.Status != GoodsReceiptStatus.Draft)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"GRN {grn.GrnNumber} is already in status {grn.Status} and cannot be posted.");
            if (grn.Delivery == null || grn.Delivery.Status != DeliveryStatus.Arrived)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse("The linked delivery must still be Arrived before posting this GRN.");
            if (grn.PurchaseOrder.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.Ordered or PurchaseOrderStatus.Arrived or PurchaseOrderStatus.Completed))
                return ApiResponse<GoodsReceiptResponse>.FailureResponse("The linked purchase order is not eligible for receiving.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            await _posting.ExecuteAsync(async () =>
            {
                grn.Status = GoodsReceiptStatus.Received;
                grn.PostedAt = now;
                grn.PostedBy = actor.AuditName;

                var po = grn.PurchaseOrder;

                foreach (var item in grn.Items)
                {
                    var poItem = po.PurchaseOrderItems.FirstOrDefault(i => i.PoItemId == item.PoItemId)
                        ?? throw new InvalidOperationException($"PO item {item.PoItemId} is missing from receipt source.");
                    if (!item.DeliveryItemId.HasValue || !await _context.DeliveryItems.AnyAsync(di => di.DeliveryItemId == item.DeliveryItemId && di.DeliveryId == grn.DeliveryId && di.PoItemId == item.PoItemId && di.ItemId == item.ItemId))
                        throw new InvalidOperationException("A GRN item no longer has a valid delivery-item source.");

                    poItem.ReceivedQuantity += item.DeliveredQuantity;
                }

                // Group batches by DeliveryItemId to validate delivery limits and create aggregated discrepancies
                var deliveryGroups = grn.Items.Where(i => i.DeliveryItemId.HasValue).GroupBy(i => i.DeliveryItemId!.Value);
                foreach (var group in deliveryGroups)
                {
                    var deliveryItemId = group.Key;
                    var firstItem = group.First();
                    var declared = firstItem.DeclaredQuantity ?? 0m;
                    var totalDeliveredInThisGrn = group.Sum(i => i.DeliveredQuantity);

                    var alreadyReceivedForDelivery = await _context.GoodsReceiptItems
                        .Where(existing => existing.DeliveryItemId == deliveryItemId
                            && existing.GoodsReceipt.GrnId != grn.GrnId
                            && existing.GoodsReceipt.Status != GoodsReceiptStatus.Draft
                            && existing.GoodsReceipt.Status != GoodsReceiptStatus.Cancelled)
                        .SumAsync(existing => (decimal?)existing.DeliveredQuantity) ?? 0m;

                    if (declared > 0 && alreadyReceivedForDelivery >= declared)
                        throw new InvalidOperationException($"This delivery item has already been fully received.");

                    // Shipment discrepancy is total actual received across all batches versus this delivery's declared quantity.
                    var variance = totalDeliveredInThisGrn - declared;

                    if (variance < 0)
                    {
                        // Shortage
                        var dscNum = await _documentNumbers.NextAsync(DocumentType.Discrepancy, now);
                        var dsc = new Discrepancy
                        {
                            DiscrepancyNumber = dscNum,
                            DiscrepancyType = DiscrepancyType.PartialShort,
                            GrnId = grn.GrnId,
                            GrnNumber = grn.GrnNumber,
                            PoId = po.PoId,
                            PoNumber = po.PoNumber,
                            DeliveryId = grn.DeliveryId,
                            DeliveryNumber = grn.Delivery?.DeliveryNumber,
                            ItemId = firstItem.ItemId,
                            OrderedQuantity = firstItem.OrderedQuantity,
                            PreviouslyReceivedQty = firstItem.PreviouslyReceivedQuantity,
                            CurrentReceivedQty = totalDeliveredInThisGrn,
                            DiscrepancyQuantity = Math.Abs(variance),
                            Status = DiscrepancyStatus.Open,
                            CreatedAt = now
                        };
                        _context.Discrepancies.Add(dsc);
                    }
                    else if (variance > 0)
                    {
                        // Over supply
                        var dscNum = await _documentNumbers.NextAsync(DocumentType.Discrepancy, now);
                        var dsc = new Discrepancy
                        {
                            DiscrepancyNumber = dscNum,
                            DiscrepancyType = DiscrepancyType.OverSupply,
                            GrnId = grn.GrnId,
                            GrnNumber = grn.GrnNumber,
                            PoId = po.PoId,
                            PoNumber = po.PoNumber,
                            DeliveryId = grn.DeliveryId,
                            DeliveryNumber = grn.Delivery?.DeliveryNumber,
                            ItemId = firstItem.ItemId,
                            OrderedQuantity = firstItem.OrderedQuantity,
                            PreviouslyReceivedQty = firstItem.PreviouslyReceivedQuantity,
                            CurrentReceivedQty = totalDeliveredInThisGrn,
                            DiscrepancyQuantity = variance,
                            Status = DiscrepancyStatus.Open,
                            CreatedAt = now
                        };
                        _context.Discrepancies.Add(dsc);
                    }
                }

                // Auto-create QualityInspection in Pending status
                var inspectionNumber = await _documentNumbers.NextAsync(DocumentType.IncomingInspection, now);
                var inspection = new QualityInspection
                {
                    InspectionNumber = inspectionNumber,
                    InspectionType = InspectionType.Incoming,
                    ReferenceType = "GRN",
                    ReferenceId = grn.GrnId,
                    ReferenceNumber = grn.GrnNumber,
                    InspectorId = actor.UserId,
                    InspectorName = actor.AuditName,
                    InspectionDate = now,
                    Status = QualityInspectionStatus.Pending,
                    TotalReceivedQuantity = grn.Items.Sum(i => i.DeliveredQuantity),
                    TotalAcceptedQuantity = 0,
                    TotalRejectedQuantity = 0,
                    OverallNotes = $"Auto-generated incoming QA inspection for {grn.GrnNumber}"
                };

                foreach (var grnItem in grn.Items)
                {
                    inspection.Items.Add(new QualityInspectionItem
                    {
                        ItemId = grnItem.ItemId,
                        Lot = grnItem.Lot,
                        DeliveredQuantity = grnItem.DeliveredQuantity,
                        AcceptedQuantity = 0,
                        RejectedQuantity = 0,
                        ConcessionQuantity = 0,
                        Notes = grnItem.Notes
                    });
                }

                _context.QualityInspections.Add(inspection);

                // Ensure Delivery is saved if linked
                if (grn.Delivery != null)
                {
                    _context.Deliveries.Update(grn.Delivery);
                }

                // Update PO status to Arrived if Pending
                if (po.Status == PurchaseOrderStatus.Pending)
                {
                    po.Status = PurchaseOrderStatus.Arrived;
                    _context.PurchaseOrders.Update(po);
                }

                _context.GoodsReceipts.Update(grn);

                _audit.Record(nameof(GoodsReceipt), grn.GrnNumber, "PostedGrn", "Status", "Draft", "Received");
                return grn;
            });

            var reloaded = await ReloadGrnAsync(grn.GrnId);
            return ApiResponse<GoodsReceiptResponse>.SuccessResponse(MapToResponse(reloaded), $"Goods Receipt Note {grn.GrnNumber} posted successfully. QA inspection initiated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting GRN {GrnId}", grnId);
            return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Failed to post GRN: {ex.Message}");
        }
    }

    public async Task<ApiResponse<GoodsReceiptResponse>> ProceedToQaAsync(int grnId)
    {
        try
        {
            var grn = await _context.GoodsReceipts.FirstOrDefaultAsync(g => g.GrnId == grnId);
            if (grn == null)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"GRN {grnId} not found.");

            if (grn.Status != GoodsReceiptStatus.Draft && grn.Status != GoodsReceiptStatus.Received)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"GRN {grn.GrnNumber} is already in status {grn.Status}.");

            var oldStatus = grn.Status.ToString();
            grn.Status = GoodsReceiptStatus.QaPending;
            await _context.SaveChangesAsync();

            _audit.Record(nameof(GoodsReceipt), grn.GrnNumber, "ProceedToQa", "Status", oldStatus, "QaPending");
            var reloaded = await ReloadGrnAsync(grn.GrnId);
            return ApiResponse<GoodsReceiptResponse>.SuccessResponse(MapToResponse(reloaded), $"GRN {grn.GrnNumber} proceeded to QA Inspection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error advancing GRN {GrnId} to QA", grnId);
            return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Failed to advance GRN to QA: {ex.Message}");
        }
    }

    public async Task<ApiResponse<GoodsReceiptResponse>> RejectGrnAsync(int grnId, RejectGrnRequest request)
    {
        try
        {
            var grn = await _context.GoodsReceipts
                .Include(g => g.PurchaseOrder)
                .Include(g => g.Delivery)
                .Include(g => g.Items)
                .FirstOrDefaultAsync(g => g.GrnId == grnId);

            if (grn == null)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"GRN {grnId} not found.");

            if (grn.Status == GoodsReceiptStatus.FullyPutAway || grn.Status == GoodsReceiptStatus.Cancelled)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"GRN {grn.GrnNumber} is {grn.Status} and cannot be rejected.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            await _posting.ExecuteAsync(async () =>
            {
                var oldStatus = grn.Status.ToString();
                grn.Status = GoodsReceiptStatus.Rejected;
                grn.RejectedBy = actor.AuditName;
                grn.RejectedAt = now;
                grn.RejectionReason = request.Reason;
                if (!string.IsNullOrWhiteSpace(request.Notes))
                {
                    grn.Notes = string.IsNullOrWhiteSpace(grn.Notes) ? request.Notes : $"{grn.Notes} | {request.Notes}";
                }

                // Automatically record Discrepancy for each item in the shipment
                foreach (var item in grn.Items)
                {
                    var dscNum = await _documentNumbers.NextAsync(DocumentType.Discrepancy, now);
                    var rejectedQty = item.DeliveredQuantity > 0 ? item.DeliveredQuantity : (item.DeclaredQuantity ?? item.OrderedQuantity);
                    var dsc = new Discrepancy
                    {
                        DiscrepancyNumber = dscNum,
                        DiscrepancyType = DiscrepancyType.Rejected,
                        GrnId = grn.GrnId,
                        GrnNumber = grn.GrnNumber,
                        PoId = grn.PoId,
                        PoNumber = grn.PurchaseOrder?.PoNumber ?? $"PO-{grn.PoId}",
                        DeliveryId = grn.DeliveryId,
                        DeliveryNumber = grn.Delivery?.DeliveryNumber,
                        ItemId = item.ItemId,
                        OrderedQuantity = item.OrderedQuantity,
                        PreviouslyReceivedQty = item.PreviouslyReceivedQuantity,
                        CurrentReceivedQty = item.DeliveredQuantity,
                        DiscrepancyQuantity = rejectedQty,
                        Status = DiscrepancyStatus.Open,
                        ResolutionNotes = $"Shipment Rejected: {request.Reason}" + (!string.IsNullOrWhiteSpace(request.Notes) ? $" - {request.Notes}" : ""),
                        CreatedAt = now
                    };
                    _context.Discrepancies.Add(dsc);
                }

                // If QA inspection exists, mark as Failed
                var inspections = await _context.QualityInspections
                    .Where(q => q.ReferenceType == "GRN" && q.ReferenceId == grn.GrnId)
                    .ToListAsync();
                foreach (var insp in inspections)
                {
                    insp.Status = QualityInspectionStatus.Failed;
                    insp.OverallNotes = $"Shipment rejected at receiving gate: {request.Reason}";
                    _context.QualityInspections.Update(insp);
                }

                _context.GoodsReceipts.Update(grn);
                _audit.Record(nameof(GoodsReceipt), grn.GrnNumber, "RejectedShipment", "Status", oldStatus, "Rejected");
                return grn;
            });

            var reloaded = await ReloadGrnAsync(grn.GrnId);
            return ApiResponse<GoodsReceiptResponse>.SuccessResponse(MapToResponse(reloaded), $"Shipment for GRN {grn.GrnNumber} rejected. Discrepancies logged.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting GRN {GrnId}", grnId);
            return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Failed to reject GRN: {ex.Message}");
        }
    }

    public async Task<ApiResponse<GoodsReceiptResponse>> CancelGrnAsync(int grnId)
    {
        try
        {
            var grn = await _context.GoodsReceipts.FirstOrDefaultAsync(g => g.GrnId == grnId);
            if (grn == null)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"GRN {grnId} not found.");

            if (grn.Status != GoodsReceiptStatus.Draft)
                return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Only draft GRNs can be cancelled. Current status is {grn.Status}.");

            grn.Status = GoodsReceiptStatus.Cancelled;
            await _context.SaveChangesAsync();

            _audit.Record(nameof(GoodsReceipt), grn.GrnNumber, "CancelledGrn", "Status", "Draft", "Cancelled");
            var reloaded = await ReloadGrnAsync(grn.GrnId);
            return ApiResponse<GoodsReceiptResponse>.SuccessResponse(MapToResponse(reloaded), $"Goods Receipt Note {grn.GrnNumber} cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling GRN {GrnId}", grnId);
            return ApiResponse<GoodsReceiptResponse>.FailureResponse($"Failed to cancel GRN: {ex.Message}");
        }
    }

    public async Task<ApiResponse<GoodsReceiptResponse>> ReceiveGoodsAsync(CreateGoodsReceiptRequest request)
    {
        return ApiResponse<GoodsReceiptResponse>.FailureResponse("Direct GRN posting is not supported. Create a draft from an arrived delivery, then post it after review.");
    }

    private async Task<GoodsReceipt> ReloadGrnAsync(int grnId)
    {
        return await _context.GoodsReceipts
            .Include(g => g.PurchaseOrder).ThenInclude(p => p.PurchaseRequisition)
            .Include(g => g.Delivery)
            .Include(g => g.Supplier)
            .Include(g => g.ReceivingLocation)
            .Include(g => g.Items).ThenInclude(i => i.Item)
            .Include(g => g.Items).ThenInclude(i => i.PurchaseUom)
            .Include(g => g.Items).ThenInclude(i => i.Lot)
            .FirstAsync(g => g.GrnId == grnId);
    }

    private static GoodsReceiptResponse MapToResponse(GoodsReceipt g) => new()
    {
        GrnId = g.GrnId,
        GrnNumber = g.GrnNumber,
        PoId = g.PoId,
        PoNumber = g.PurchaseOrder?.PoNumber ?? $"PO-{g.PoId}",
        PrId = g.PurchaseOrder?.PrId,
        PrNumber = g.PurchaseOrder?.PurchaseRequisition?.PrNumber,
        DeliveryId = g.DeliveryId,
        DeliveryNumber = g.Delivery?.DeliveryNumber,
        SupplierId = g.SupplierId,
        SupplierName = g.Supplier?.CompanyName ?? string.Empty,
        ReceivingLocationId = g.ReceivingLocationId,
        ReceivingLocationName = g.ReceivingLocation?.LocationName ?? string.Empty,
        ReceivedDate = g.ReceivedDate,
        DeliveryNoteNumber = g.DeliveryNoteNumber,
        SupplierDrNumber = g.SupplierDrNumber,
        SupplierInvoiceNumber = g.SupplierInvoiceNumber,
        ReceivingBay = g.ReceivingBay,
        Carrier = g.Carrier,
        ReceivedBy = g.ReceivedBy,
        CreatedBy = g.CreatedBy,
        CreatedAt = g.CreatedAt,
        PostedBy = g.PostedBy,
        PostedAt = g.PostedAt,
        Status = EnumDbValue.ToDbValue(g.Status),
        RejectedBy = g.RejectedBy,
        RejectedAt = g.RejectedAt,
        RejectionReason = g.RejectionReason,
        Notes = g.Notes,
        Items = g.Items.Select(i => new GoodsReceiptItemResponse
        {
            GrnItemId = i.GrnItemId,
            PoItemId = i.PoItemId,
            ItemId = i.ItemId,
            ItemName = i.Item?.ItemName ?? $"Item {i.ItemId}",
            OrderedQuantity = i.OrderedQuantity,
            PreviouslyReceivedQuantity = i.PreviouslyReceivedQuantity,
            DeclaredQuantity = i.DeclaredQuantity,
            DeliveredQuantity = i.DeliveredQuantity,
            VarianceQuantity = i.VarianceQuantity,
            VarianceType = i.VarianceType,
            DeliveryItemId = i.DeliveryItemId,
            PurchaseUomId = i.PurchaseUomId,
            PurchaseUomName = i.PurchaseUom?.Abbreviation ?? "Unit",
            LotId = i.LotId,
            LotCode = i.Lot?.LotCode,
            SupplierLotCode = i.SupplierLotCode,
            ManufactureDate = i.ManufactureDate,
            ExpiryDate = i.ExpiryDate,
            Notes = i.Notes
        }).ToList()
    };
}
