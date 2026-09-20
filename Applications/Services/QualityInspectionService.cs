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

public class QualityInspectionService : IQualityInspectionService
{
    private readonly ScmDbContext _context;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly IPostingTransaction _posting;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<QualityInspectionService> _logger;

    public QualityInspectionService(
        ScmDbContext context,
        IDocumentNumberService documentNumbers,
        IPostingTransaction posting,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<QualityInspectionService> logger)
    {
        _context = context;
        _documentNumbers = documentNumbers;
        _posting = posting;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<List<QualityInspectionResponse>>> GetInspectionsAsync(string? inspectionType = null)
    {
        try
        {
            var query = _context.QualityInspections
                .Include(q => q.Items).ThenInclude(i => i.Item)
                .Include(q => q.Items).ThenInclude(i => i.Lot)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(inspectionType) && !inspectionType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (EnumDbValue.TryParse<InspectionType>(inspectionType, out var it))
                    query = query.Where(q => q.InspectionType == it);
            }

            var list = await query.OrderByDescending(q => q.InspectionId).ToListAsync();
            return ApiResponse<List<QualityInspectionResponse>>.SuccessResponse(list.Select(MapToResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quality inspections");
            return ApiResponse<List<QualityInspectionResponse>>.FailureResponse("An error occurred while fetching quality inspections.");
        }
    }

    public async Task<ApiResponse<QualityInspectionResponse>> GetInspectionByIdAsync(int inspectionId)
    {
        try
        {
            var qc = await _context.QualityInspections
                .Include(q => q.Items).ThenInclude(i => i.Item)
                .Include(q => q.Items).ThenInclude(i => i.Lot)
                .FirstOrDefaultAsync(q => q.InspectionId == inspectionId);

            if (qc == null)
                return ApiResponse<QualityInspectionResponse>.FailureResponse($"Inspection {inspectionId} not found.");

            return ApiResponse<QualityInspectionResponse>.SuccessResponse(MapToResponse(qc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspection {InspectionId}", inspectionId);
            return ApiResponse<QualityInspectionResponse>.FailureResponse("An error occurred while fetching inspection.");
        }
    }

    public async Task<ApiResponse<QualityInspectionResponse>> InspectIncomingGoodsAsync(CreateQualityInspectionRequest request)
    {
        try
        {
            if (request.Items == null || !request.Items.Any())
                return ApiResponse<QualityInspectionResponse>.FailureResponse("Inspection must contain at least one item.");

            var grn = await _context.GoodsReceipts
                .Include(g => g.PurchaseOrder)
                .Include(g => g.Items)
                .FirstOrDefaultAsync(g => g.GrnId == request.ReferenceId);

            if (grn == null)
                return ApiResponse<QualityInspectionResponse>.FailureResponse($"Goods receipt {request.ReferenceId} not found.");

            var actor = _currentUser.Current;
            var inspectDate = DateTime.UtcNow;

            var inspectionNumber = await _documentNumbers.NextAsync(DocumentType.IncomingInspection, inspectDate);

            var inspection = new QualityInspection
            {
                InspectionNumber = inspectionNumber,
                InspectionType = InspectionType.Incoming,
                ReferenceType = "GRN",
                ReferenceId = grn.GrnId,
                ReferenceNumber = grn.GrnNumber,
                InspectorId = actor.UserId,
                InspectorName = actor.AuditName,
                InspectionDate = inspectDate,
                OverallNotes = request.OverallNotes
            };

            var qcItems = new List<QualityInspectionItem>();
            var anyRejected = false;
            var anyConcession = false;

            await _posting.ExecuteAsync(async () =>
            {
                foreach (var itemReq in request.Items)
                {
                    var lot = await _context.InventoryLots.FindAsync(itemReq.LotId);
                    if (lot == null)
                        throw new InvalidOperationException($"Lot {itemReq.LotId} not found.");

                    var accepted = itemReq.AcceptedQuantity;
                    var rejected = itemReq.RejectedQuantity;
                    var concession = itemReq.ConcessionQuantity;

                    if (rejected > 0) anyRejected = true;
                    if (concession > 0) anyConcession = true;

                    // Release stock to Available for accepted/concession quantities
                    if (rejected == 0)
                    {
                        lot.Status = LotStatus.Available;
                        // Increase available balance cache
                        await AdjustInventoryCacheAsync(lot.ItemId, lot.LocationId, lot.QuantityRemaining);
                    }
                    else if (accepted > 0 && rejected > 0)
                    {
                        // Split lot: keep accepted portion on the main lot, release to available
                        lot.QuantityRemaining = accepted;
                        lot.Status = LotStatus.Available;
                        await AdjustInventoryCacheAsync(lot.ItemId, lot.LocationId, accepted);

                        // Create separate rejected lot for the rejected portion
                        var rejLot = new InventoryLot
                        {
                            LotCode = $"{lot.LotCode}-REJ",
                            ItemId = lot.ItemId,
                            LocationId = lot.LocationId,
                            SourceType = lot.SourceType,
                            SupplierId = lot.SupplierId,
                            SupplierLotNo = lot.SupplierLotNo,
                            ReceivedDate = lot.ReceivedDate,
                            ManufactureDate = lot.ManufactureDate,
                            ExpiryDate = lot.ExpiryDate,
                            QuantityReceived = rejected,
                            QuantityRemaining = rejected,
                            UomId = lot.UomId,
                            UnitCost = lot.UnitCost,
                            Status = LotStatus.Rejected
                        };
                        _context.InventoryLots.Add(rejLot);
                    }
                    else
                    {
                        // All rejected
                        lot.Status = LotStatus.Rejected;
                    }

                    _context.InventoryLots.Update(lot);

                    qcItems.Add(new QualityInspectionItem
                    {
                        ItemId = itemReq.ItemId,
                        LotId = itemReq.LotId,
                        DeliveredQuantity = itemReq.DeliveredQuantity,
                        AcceptedQuantity = accepted,
                        RejectedQuantity = rejected,
                        ConcessionQuantity = concession,
                        DefectReason = itemReq.DefectReason,
                        Notes = itemReq.Notes
                    });

                    // If rejected, automatically generate Non-Conformance Report (NCR)
                    if (rejected > 0)
                    {
                        var ncrNumber = await _documentNumbers.NextAsync(DocumentType.NonConformance, inspectDate);
                        var ncr = new NonConformanceReport
                        {
                            NcrNumber = ncrNumber,
                            Inspection = inspection,
                            SupplierId = grn.SupplierId,
                            ItemId = itemReq.ItemId,
                            LotId = lot.LotId,
                            DefectiveQuantity = rejected,
                            DefectType = itemReq.DefectReason ?? "Incoming QC Defect",
                            Severity = "Major",
                            RootCause = "Failed quality inspection criteria upon receipt.",
                            Status = NcrStatus.Open,
                            CreatedBy = actor.AuditName,
                            CreatedAt = inspectDate
                        };
                        _context.NonConformanceReports.Add(ncr);
                    }
                }

                inspection.Status = anyRejected
                    ? QualityInspectionStatus.Failed
                    : (anyConcession ? QualityInspectionStatus.PassedWithConcession : QualityInspectionStatus.Passed);

                inspection.Items = qcItems;
                _context.QualityInspections.Add(inspection);

                // Update GRN status
                grn.Status = GoodsReceiptStatus.Inspected;
                _context.GoodsReceipts.Update(grn);

                // Update PO status to Completed
                if (grn.PurchaseOrder != null && grn.PurchaseOrder.Status == PurchaseOrderStatus.Arrived)
                {
                    grn.PurchaseOrder.Status = PurchaseOrderStatus.Completed;
                    grn.PurchaseOrder.QaStatus = EnumDbValue.ToDbValue(inspection.Status);
                    grn.PurchaseOrder.QaInspectedDate = inspectDate;
                    grn.PurchaseOrder.InspectedBy = actor.AuditName;
                    grn.PurchaseOrder.QaNotes = inspection.OverallNotes;
                    _context.PurchaseOrders.Update(grn.PurchaseOrder);
                }

                _audit.Record(
                    nameof(QualityInspection),
                    inspection.InspectionNumber,
                    "InspectionCompleted",
                    fieldName: "Status",
                    oldValue: null,
                    newValue: EnumDbValue.ToDbValue(inspection.Status));

                return inspection;
            });

            var reloaded = await _context.QualityInspections
                .Include(q => q.Items).ThenInclude(i => i.Item)
                .Include(q => q.Items).ThenInclude(i => i.Lot)
                .FirstAsync(q => q.InspectionId == inspection.InspectionId);

            return ApiResponse<QualityInspectionResponse>.SuccessResponse(
                MapToResponse(reloaded),
                $"Quality Inspection {inspection.InspectionNumber} completed: {EnumDbValue.ToDbValue(inspection.Status)}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing quality inspection for GRN {GrnId}", request.ReferenceId);
            return ApiResponse<QualityInspectionResponse>.FailureResponse($"Failed to perform quality inspection: {ex.Message}");
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

    private static QualityInspectionResponse MapToResponse(QualityInspection q) => new()
    {
        InspectionId = q.InspectionId,
        InspectionNumber = q.InspectionNumber,
        InspectionType = EnumDbValue.ToDbValue(q.InspectionType),
        ReferenceType = q.ReferenceType,
        ReferenceId = q.ReferenceId,
        ReferenceNumber = q.ReferenceNumber,
        InspectorId = q.InspectorId,
        InspectorName = q.InspectorName,
        InspectionDate = q.InspectionDate,
        Status = EnumDbValue.ToDbValue(q.Status),
        OverallNotes = q.OverallNotes,
        Items = q.Items.Select(i => new QualityInspectionItemResponse
        {
            InspectionItemId = i.InspectionItemId,
            ItemId = i.ItemId,
            ItemName = i.Item?.ItemName ?? $"Item {i.ItemId}",
            LotId = i.LotId,
            LotCode = i.Lot?.LotCode,
            DeliveredQuantity = i.DeliveredQuantity,
            AcceptedQuantity = i.AcceptedQuantity,
            RejectedQuantity = i.RejectedQuantity,
            ConcessionQuantity = i.ConcessionQuantity,
            DefectReason = i.DefectReason,
            Notes = i.Notes
        }).ToList()
    };
}
