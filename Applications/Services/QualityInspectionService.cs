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

    public async Task<ApiResponse<List<QualityInspectionResponse>>> GetInspectionsAsync(string? inspectionType = null, int? grnId = null, string? status = null)
    {
        try
        {
            var query = _context.QualityInspections
                .Include(q => q.Items).ThenInclude(i => i.Item)
                .Include(q => q.Items).ThenInclude(i => i.Item).ThenInclude(i => i.Category)
                .Include(q => q.Items).ThenInclude(i => i.Lot)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(inspectionType) && !inspectionType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (EnumDbValue.TryParse<InspectionType>(inspectionType, out var it))
                    query = query.Where(q => q.InspectionType == it);
            }

            if (grnId.HasValue)
            {
                query = query.Where(q => q.ReferenceType == "GRN" && q.ReferenceId == grnId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (EnumDbValue.TryParse<QualityInspectionStatus>(status, out var parsedStatus))
                    query = query.Where(q => q.Status == parsedStatus);
            }

            var list = await query.OrderByDescending(q => q.InspectionId).ToListAsync();
            var enriched = await EnrichInspectionResponsesAsync(list);
            return ApiResponse<List<QualityInspectionResponse>>.SuccessResponse(enriched);
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
                .Include(q => q.Items).ThenInclude(i => i.Item).ThenInclude(i => i.Category)
                .Include(q => q.Items).ThenInclude(i => i.Lot)
                .FirstOrDefaultAsync(q => q.InspectionId == inspectionId);

            if (qc == null)
                return ApiResponse<QualityInspectionResponse>.FailureResponse($"Inspection {inspectionId} not found.");

            var enriched = await EnrichInspectionResponsesAsync(new List<QualityInspection> { qc });
            return ApiResponse<QualityInspectionResponse>.SuccessResponse(enriched[0]);
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

            foreach (var itemReq in request.Items)
            {
                inspection.Items.Add(new QualityInspectionItem
                {
                    ItemId = itemReq.ItemId,
                    LotId = itemReq.LotId,
                    DeliveredQuantity = itemReq.DeliveredQuantity,
                    AcceptedQuantity = itemReq.AcceptedQuantity,
                    RejectedQuantity = itemReq.RejectedQuantity,
                    ConcessionQuantity = itemReq.ConcessionQuantity,
                    DefectReason = itemReq.DefectReason,
                    Notes = itemReq.Notes
                });
            }

            _context.QualityInspections.Add(inspection);
            await _context.SaveChangesAsync();

            var completeReq = new CompleteQualityInspectionRequest
            {
                OverallNotes = request.OverallNotes,
                Items = request.Items.Select(i => new CompleteQualityInspectionItemRequest
                {
                    ItemId = i.ItemId,
                    LotId = i.LotId,
                    DeliveredQuantity = i.DeliveredQuantity,
                    AcceptedQuantity = i.AcceptedQuantity,
                    RejectedQuantity = i.RejectedQuantity,
                    ConcessionQuantity = i.ConcessionQuantity,
                    DefectReason = i.DefectReason,
                    Notes = i.Notes
                }).ToList()
            };

            return await CompleteInspectionAsync(inspection.InspectionId, completeReq);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating QA inspection for ReferenceId {ReferenceId}", request.ReferenceId);
            return ApiResponse<QualityInspectionResponse>.FailureResponse($"Failed to create QA inspection: {ex.Message}");
        }
    }

    public async Task<ApiResponse<QualityInspectionResponse>> CompleteInspectionAsync(int inspectionId, CompleteQualityInspectionRequest request)
    {
        try
        {
            var inspection = await _context.QualityInspections
                .Include(q => q.Items).ThenInclude(i => i.Item)
                .Include(q => q.Items).ThenInclude(i => i.Lot)
                .FirstOrDefaultAsync(q => q.InspectionId == inspectionId);

            if (inspection == null)
                return ApiResponse<QualityInspectionResponse>.FailureResponse($"Inspection {inspectionId} not found.");

            if (inspection.Status != QualityInspectionStatus.Pending)
                return ApiResponse<QualityInspectionResponse>.FailureResponse(
                    $"Inspection {inspection.InspectionNumber} is already {inspection.Status} and cannot be completed again.");

            if (request.Items == null || !request.Items.Any())
                return ApiResponse<QualityInspectionResponse>.FailureResponse("At least one item must be inspected.");

            var submittedInspectionItemIds = request.Items
                .Where(item => item.InspectionItemId.HasValue)
                .Select(item => item.InspectionItemId!.Value)
                .ToHashSet();
            var submittedItemIds = request.Items.Select(item => item.ItemId).ToHashSet();
            if (inspection.Items.Any(item =>
                !submittedInspectionItemIds.Contains(item.InspectionItemId) &&
                !submittedItemIds.Contains(item.ItemId)))
                return ApiResponse<QualityInspectionResponse>.FailureResponse(
                    "Every received item must be included in the quality inspection.");

            // Business rule: Accepted + Rejected must equal Delivered per item
            foreach (var itemReq in request.Items)
            {
                var qcItem = itemReq.InspectionItemId.HasValue
                    ? inspection.Items.FirstOrDefault(i => i.InspectionItemId == itemReq.InspectionItemId.Value)
                    : inspection.Items.FirstOrDefault(i => i.ItemId == itemReq.ItemId);

                if (qcItem != null)
                {
                    if (itemReq.ItemId == 0) itemReq.ItemId = qcItem.ItemId;
                    if (itemReq.DeliveredQuantity <= 0) itemReq.DeliveredQuantity = qcItem.DeliveredQuantity;
                    if (!itemReq.LotId.HasValue) itemReq.LotId = qcItem.LotId;
                }

                if (itemReq.AcceptedQuantity + itemReq.RejectedQuantity != itemReq.DeliveredQuantity)
                {
                    return ApiResponse<QualityInspectionResponse>.FailureResponse(
                        $"Item {itemReq.ItemId}: Accepted ({itemReq.AcceptedQuantity}) + Rejected ({itemReq.RejectedQuantity}) must equal Delivered quantity ({itemReq.DeliveredQuantity}).");
                }

                if (itemReq.RejectedQuantity > 0 && string.IsNullOrWhiteSpace(itemReq.DefectReason))
                {
                    return ApiResponse<QualityInspectionResponse>.FailureResponse(
                        $"Item {itemReq.ItemId}: a defect reason is required when rejected quantity is greater than zero.");
                }
            }

            var grn = await _context.GoodsReceipts
                .Include(g => g.PurchaseOrder)
                .Include(g => g.Delivery)
                .Include(g => g.Items)
                .FirstOrDefaultAsync(g => g.GrnId == inspection.ReferenceId);

            if (grn == null)
                return ApiResponse<QualityInspectionResponse>.FailureResponse($"Goods receipt note for inspection {inspectionId} not found.");

            var actor = _currentUser.Current;
            var inspectDate = DateTime.UtcNow;

            await _posting.ExecuteAsync(async () =>
            {
                var anyRejected = false;
                var anyConcession = false;
                decimal totalDelivered = 0;
                decimal totalAccepted = 0;
                decimal totalRejected = 0;

                foreach (var itemReq in request.Items)
                {
                    var qcItem = itemReq.InspectionItemId.HasValue
                        ? inspection.Items.FirstOrDefault(i => i.InspectionItemId == itemReq.InspectionItemId.Value)
                        : inspection.Items.FirstOrDefault(i => i.ItemId == itemReq.ItemId);
                    var grnItem = grn.Items.FirstOrDefault(i => i.ItemId == itemReq.ItemId);

                    var accepted = itemReq.AcceptedQuantity;
                    var rejected = itemReq.RejectedQuantity;
                    var concession = itemReq.ConcessionQuantity;

                    totalDelivered += itemReq.DeliveredQuantity;
                    totalAccepted += accepted;
                    totalRejected += rejected;

                    if (rejected > 0) anyRejected = true;
                    if (concession > 0) anyConcession = true;

                    InventoryLot? lot = null;
                    if (itemReq.LotId.HasValue)
                    {
                        lot = await _context.InventoryLots.FindAsync(itemReq.LotId.Value);
                    }
                    else if (qcItem?.LotId.HasValue == true)
                    {
                        lot = await _context.InventoryLots.FindAsync(qcItem.LotId.Value);
                    }
                    else if (grnItem?.LotId.HasValue == true)
                    {
                        lot = await _context.InventoryLots.FindAsync(grnItem.LotId.Value);
                    }

                    InventoryLot? acceptedLot = lot;
                    InventoryLot? rejectedLot = null;

                    if (lot != null)
                    {
                        if (rejected == 0)
                        {
                            // Entire lot accepted - remains in Quarantine until Put Away confirms location
                            lot.Status = LotStatus.Quarantine;
                            acceptedLot = lot;
                        }
                        else if (accepted > 0 && rejected > 0)
                        {
                            // Split lot: accepted portion continues to Put Away
                            lot.QuantityRemaining = accepted;
                            lot.QuantityReceived = accepted;
                            lot.Status = LotStatus.Quarantine;
                            acceptedLot = lot;

                            // Create separate rejected lot
                            rejectedLot = new InventoryLot
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
                            _context.InventoryLots.Add(rejectedLot);
                        }
                        else
                        {
                            // All rejected
                            lot.Status = LotStatus.Rejected;
                            rejectedLot = lot;
                            acceptedLot = null;
                        }

                        _context.InventoryLots.Update(lot);
                    }

                    if (qcItem != null)
                    {
                        qcItem.AcceptedQuantity = accepted;
                        qcItem.RejectedQuantity = rejected;
                        qcItem.ConcessionQuantity = concession;
                        qcItem.DefectReason = itemReq.DefectReason;
                        qcItem.Notes = itemReq.Notes;
                    }
                    else
                    {
                        inspection.Items.Add(new QualityInspectionItem
                        {
                            ItemId = itemReq.ItemId,
                            Lot = acceptedLot ?? lot,
                            DeliveredQuantity = itemReq.DeliveredQuantity,
                            AcceptedQuantity = accepted,
                            RejectedQuantity = rejected,
                            ConcessionQuantity = concession,
                            DefectReason = itemReq.DefectReason,
                            Notes = itemReq.Notes
                        });
                    }

                    // Auto-create NCR and Rejected Discrepancy for any rejected quantities
                    if (rejected > 0)
                    {
                        var ncrNumber = await _documentNumbers.NextAsync(DocumentType.NonConformance, inspectDate);
                        var ncr = new NonConformanceReport
                        {
                            NcrNumber = ncrNumber,
                            Inspection = inspection,
                            SupplierId = grn.SupplierId,
                            ItemId = itemReq.ItemId,
                            Lot = rejectedLot ?? lot,
                            DefectiveQuantity = rejected,
                            DefectType = itemReq.DefectReason ?? "Incoming QC Defect",
                            Severity = "Major",
                            RootCause = "Failed quality inspection criteria upon receipt.",
                            Status = NcrStatus.Open,
                            Disposition = "Pending",
                            CreatedBy = actor.AuditName,
                            CreatedAt = inspectDate
                        };
                        _context.NonConformanceReports.Add(ncr);

                        var dscNumber = await _documentNumbers.NextAsync(DocumentType.Discrepancy, inspectDate);
                        var dsc = new Discrepancy
                        {
                            DiscrepancyNumber = dscNumber,
                            DiscrepancyType = DiscrepancyType.Rejected,
                            GrnId = grn.GrnId,
                            GrnNumber = grn.GrnNumber,
                            PoId = grn.PoId,
                            PoNumber = grn.PurchaseOrder.PoNumber,
                            DeliveryId = grn.DeliveryId,
                            DeliveryNumber = grn.Delivery?.DeliveryNumber,
                            ItemId = itemReq.ItemId,
                            OrderedQuantity = grnItem?.OrderedQuantity ?? itemReq.DeliveredQuantity,
                            PreviouslyReceivedQty = grnItem?.PreviouslyReceivedQuantity ?? 0,
                            CurrentReceivedQty = itemReq.DeliveredQuantity,
                            DiscrepancyQuantity = rejected,
                            Status = DiscrepancyStatus.Open,
                            NonConformanceReport = ncr,
                            CreatedAt = inspectDate
                        };
                        _context.Discrepancies.Add(dsc);
                    }

                    // Auto-create Put Away task for accepted quantity
                    if (accepted > 0 && grnItem != null)
                    {
                        var paNumber = await _documentNumbers.NextAsync(DocumentType.PutAway, inspectDate);
                        var pa = new PutAwayTransaction
                        {
                            PutAwayNumber = paNumber,
                            GrnId = grn.GrnId,
                            GrnItemId = grnItem.GrnItemId,
                            QaInspectionId = inspection.InspectionId,
                            ItemId = itemReq.ItemId,
                            AcceptedQuantity = accepted,
                            UomId = grnItem.PurchaseUomId,
                            Lot = acceptedLot ?? lot,
                            LotCode = acceptedLot?.LotCode ?? lot?.LotCode,
                            ExpiryDate = acceptedLot?.ExpiryDate ?? lot?.ExpiryDate,
                            Status = PutAwayStatus.Pending,
                            CreatedAt = inspectDate
                        };
                        _context.PutAwayTransactions.Add(pa);
                    }
                }

                inspection.TotalReceivedQuantity = totalDelivered;
                inspection.TotalAcceptedQuantity = totalAccepted;
                inspection.TotalRejectedQuantity = totalRejected;
                inspection.CompletedAt = inspectDate;
                inspection.CompletedBy = actor.AuditName;
                if (!string.IsNullOrWhiteSpace(request.OverallNotes))
                    inspection.OverallNotes = request.OverallNotes;

                inspection.Status = (totalAccepted == 0)
                    ? QualityInspectionStatus.Failed
                    : (anyRejected ? QualityInspectionStatus.PassedWithConcession : QualityInspectionStatus.Passed);

                _context.QualityInspections.Update(inspection);

                // Update GRN status
                grn.Status = GoodsReceiptStatus.QaCompleted;
                _context.GoodsReceipts.Update(grn);

                _audit.Record(nameof(QualityInspection), inspection.InspectionNumber, "InspectionCompleted", "Status", "Pending", inspection.Status.ToString());
                return inspection;
            });

            var reloaded = await _context.QualityInspections
                .Include(q => q.Items).ThenInclude(i => i.Item)
                .Include(q => q.Items).ThenInclude(i => i.Item).ThenInclude(i => i.Category)
                .Include(q => q.Items).ThenInclude(i => i.Lot)
                .FirstAsync(q => q.InspectionId == inspection.InspectionId);

            var enriched = await EnrichInspectionResponsesAsync(new List<QualityInspection> { reloaded });

            return ApiResponse<QualityInspectionResponse>.SuccessResponse(
                enriched[0],
                $"Quality Inspection {inspection.InspectionNumber} completed with status: {reloaded.Status}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing inspection {InspectionId}", inspectionId);
            return ApiResponse<QualityInspectionResponse>.FailureResponse($"Failed to complete inspection: {ex.Message}");
        }
    }

    private async Task<List<QualityInspectionResponse>> EnrichInspectionResponsesAsync(List<QualityInspection> list)
    {
        var grnIds = list.Where(q => q.ReferenceType == "GRN").Select(q => q.ReferenceId).Distinct().ToList();
        var grnLookup = await _context.GoodsReceipts
            .Include(g => g.PurchaseOrder).ThenInclude(p => p.PurchaseRequisition)
            .Include(g => g.Supplier)
            .Where(g => grnIds.Contains(g.GrnId))
            .ToDictionaryAsync(g => g.GrnId);

        return list.Select(q =>
        {
            var resp = MapToResponse(q);
            if (q.ReferenceType == "GRN" && grnLookup.TryGetValue(q.ReferenceId, out var grn))
            {
                resp.PoId = grn.PoId;
                resp.PoNumber = grn.PurchaseOrder?.PoNumber;
                resp.PrId = grn.PurchaseOrder?.PrId;
                resp.PrNumber = grn.PurchaseOrder?.PurchaseRequisition?.PrNumber;
                resp.SupplierName = grn.Supplier?.CompanyName;
            }
            return resp;
        }).ToList();
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
        TotalReceivedQuantity = q.TotalReceivedQuantity,
        TotalAcceptedQuantity = q.TotalAcceptedQuantity,
        TotalRejectedQuantity = q.TotalRejectedQuantity,
        CompletedAt = q.CompletedAt,
        CompletedBy = q.CompletedBy,
        OverallNotes = q.OverallNotes,
        Items = q.Items.Select(i => new QualityInspectionItemResponse
        {
            InspectionItemId = i.InspectionItemId,
            ItemId = i.ItemId,
            ItemName = i.Item?.ItemName ?? $"Item {i.ItemId}",
            CategoryName = i.Item?.Category?.CategoryName ?? "Raw Materials",
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
