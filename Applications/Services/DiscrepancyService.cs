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

public class DiscrepancyService : IDiscrepancyService
{
    private readonly ScmDbContext _context;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly IPostingTransaction _posting;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<DiscrepancyService> _logger;

    public DiscrepancyService(
        ScmDbContext context,
        IDocumentNumberService documentNumbers,
        IPostingTransaction posting,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<DiscrepancyService> logger)
    {
        _context = context;
        _documentNumbers = documentNumbers;
        _posting = posting;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<List<DiscrepancyResponse>>> GetDiscrepanciesAsync(
        string? type = null,
        string? status = null,
        int? grnId = null,
        int? poId = null)
    {
        try
        {
            var query = _context.Discrepancies
                .Include(d => d.Item)
                .Include(d => d.PurchaseOrder).ThenInclude(p => p.PurchaseRequisition)
                .Include(d => d.PurchaseOrder).ThenInclude(p => p.Supplier)
                .Include(d => d.GoodsReceipt)
                .Include(d => d.Delivery)
                .Include(d => d.LossReport)
                .Include(d => d.ReturnToVendor)
                .Include(d => d.NonConformanceReport)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(type) && !type.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (EnumDbValue.TryParse<DiscrepancyType>(type, out var dt))
                    query = query.Where(d => d.DiscrepancyType == dt);
            }

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (EnumDbValue.TryParse<DiscrepancyStatus>(status, out var ds))
                    query = query.Where(d => d.Status == ds);
            }

            if (grnId.HasValue)
                query = query.Where(d => d.GrnId == grnId.Value);

            if (poId.HasValue)
                query = query.Where(d => d.PoId == poId.Value);

            var list = await query.OrderByDescending(d => d.DiscrepancyId).ToListAsync();
            return ApiResponse<List<DiscrepancyResponse>>.SuccessResponse(list.Select(MapToResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving discrepancies");
            return ApiResponse<List<DiscrepancyResponse>>.FailureResponse("An error occurred while fetching discrepancies.");
        }
    }

    public async Task<ApiResponse<DiscrepancyResponse>> GetDiscrepancyByIdAsync(int id)
    {
        try
        {
            var dsc = await _context.Discrepancies
                .Include(d => d.Item)
                .Include(d => d.PurchaseOrder).ThenInclude(p => p.PurchaseRequisition)
                .Include(d => d.PurchaseOrder).ThenInclude(p => p.Supplier)
                .Include(d => d.GoodsReceipt)
                .Include(d => d.Delivery)
                .Include(d => d.LossReport)
                .Include(d => d.ReturnToVendor)
                .Include(d => d.NonConformanceReport)
                .FirstOrDefaultAsync(d => d.DiscrepancyId == id);

            if (dsc == null)
                return ApiResponse<DiscrepancyResponse>.FailureResponse($"Discrepancy {id} not found.");

            return ApiResponse<DiscrepancyResponse>.SuccessResponse(MapToResponse(dsc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving discrepancy {Id}", id);
            return ApiResponse<DiscrepancyResponse>.FailureResponse("An error occurred while fetching discrepancy.");
        }
    }

    public async Task<ApiResponse<DiscrepancyResponse>> ResolveAsync(int id, ResolveDiscrepancyRequest request)
    {
        try
        {
            var dsc = await _context.Discrepancies
                .Include(d => d.Item)
                .Include(d => d.PurchaseOrder)
                .Include(d => d.GoodsReceipt)
                .FirstOrDefaultAsync(d => d.DiscrepancyId == id);

            if (dsc == null)
                return ApiResponse<DiscrepancyResponse>.FailureResponse($"Discrepancy {id} not found.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            dsc.Status = DiscrepancyStatus.Resolved;
            dsc.ResolutionType = request.ResolutionType;
            dsc.ResolutionNotes = request.ResolutionNotes;
            dsc.ResolvedBy = actor.AuditName;
            dsc.ResolvedAt = now;

            await _context.SaveChangesAsync();

            _audit.Record(nameof(Discrepancy), dsc.DiscrepancyNumber, "Resolved", "Status", "Open", "Resolved");

            return await GetDiscrepancyByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving discrepancy {Id}", id);
            return ApiResponse<DiscrepancyResponse>.FailureResponse($"Failed to resolve discrepancy: {ex.Message}");
        }
    }

    public async Task<ApiResponse<LossReportResponse>> CreateLossReportAsync(int discrepancyId, CreateLossReportRequest request)
    {
        try
        {
            var dsc = await _context.Discrepancies
                .Include(d => d.Item)
                .Include(d => d.PurchaseOrder)
                .Include(d => d.GoodsReceipt).ThenInclude(g => g.Items)
                .Include(d => d.NonConformanceReport)
                .FirstOrDefaultAsync(d => d.DiscrepancyId == discrepancyId);

            if (dsc == null)
                return ApiResponse<LossReportResponse>.FailureResponse($"Discrepancy {discrepancyId} not found.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            var lrNumber = await _documentNumbers.NextAsync(DocumentType.LossReport, now);

            // Find rejected lot if this was a rejection
            InventoryLot? lot = null;
            if (dsc.NonConformanceReport?.LotId.HasValue == true)
            {
                lot = await _context.InventoryLots.FindAsync(dsc.NonConformanceReport.LotId.Value);
            }
            else
            {
                var grnItem = dsc.GoodsReceipt?.Items.FirstOrDefault(i => i.ItemId == dsc.ItemId);
                if (grnItem?.LotId.HasValue == true)
                {
                    lot = await _context.InventoryLots.FindAsync(grnItem.LotId.Value);
                }
            }

            var lossReport = new LossReport
            {
                LossReportNumber = lrNumber,
                DiscrepancyId = dsc.DiscrepancyId,
                GrnId = dsc.GrnId,
                PoId = dsc.PoId,
                LotId = lot?.LotId,
                ItemId = dsc.ItemId,
                LostQuantity = dsc.DiscrepancyQuantity,
                UomId = dsc.Item.StockUomId,
                Reason = request.Reason,
                Notes = request.Notes,
                AuthorisedBy = request.AuthorisedBy,
                CreatedBy = actor.AuditName,
                CreatedAt = now
            };

            await _posting.ExecuteAsync(async () =>
            {
                if (lot != null)
                {
                    lot.Status = LotStatus.Disposed;
                    _context.InventoryLots.Update(lot);

                    // Record disposal in stock ledger
                    var ledger = new StockLedger
                    {
                        LotId = lot.LotId,
                        ItemId = lot.ItemId,
                        LocationId = lot.LocationId,
                        MovementType = MovementType.Disposal,
                        Quantity = -dsc.DiscrepancyQuantity,
                        UomId = lot.UomId,
                        UnitCost = lot.UnitCost,
                        PostedAt = now,
                        ReferenceType = "LossReport",
                        ReferenceId = lrNumber,
                        UserId = actor.UserId,
                        UserName = actor.AuditName,
                        Notes = $"Written off via Loss Report {lrNumber}: {request.Reason}"
                    };
                    _context.StockLedgers.Add(ledger);
                    lossReport.StockLedgerEntry = ledger;
                }

                _context.LossReports.Add(lossReport);

                // Update discrepancy
                dsc.Status = DiscrepancyStatus.Resolved;
                dsc.ResolutionType = "LossReport";
                dsc.LossReport = lossReport;
                dsc.ResolvedBy = actor.AuditName;
                dsc.ResolvedAt = now;
                _context.Discrepancies.Update(dsc);

                // Update NCR if linked
                if (dsc.NonConformanceReport != null)
                {
                    dsc.NonConformanceReport.Disposition = "LossReport";
                    dsc.NonConformanceReport.DispositionNotes = request.Notes;
                    dsc.NonConformanceReport.DispositionBy = actor.AuditName;
                    dsc.NonConformanceReport.DispositionAt = now;
                    dsc.NonConformanceReport.Status = NcrStatus.Closed;
                    _context.NonConformanceReports.Update(dsc.NonConformanceReport);
                }

                _audit.Record(nameof(LossReport), lrNumber, "Created", "Status", null, "Authorised");
                return lossReport;
            });

            var response = new LossReportResponse
            {
                LossReportId = lossReport.LossReportId,
                LossReportNumber = lossReport.LossReportNumber,
                DiscrepancyId = dsc.DiscrepancyId,
                DiscrepancyNumber = dsc.DiscrepancyNumber,
                GrnId = dsc.GrnId,
                GrnNumber = dsc.GrnNumber,
                PoId = dsc.PoId,
                PoNumber = dsc.PoNumber,
                ItemId = dsc.ItemId,
                ItemName = dsc.Item.ItemName,
                LostQuantity = lossReport.LostQuantity,
                UomId = lossReport.UomId,
                UomName = dsc.Item.Uom?.Abbreviation ?? "Unit",
                Reason = lossReport.Reason,
                Notes = lossReport.Notes,
                IsAcknowledged = false,
                AuthorisedBy = lossReport.AuthorisedBy,
                CreatedBy = lossReport.CreatedBy,
                CreatedAt = lossReport.CreatedAt,
                StockLedgerEntryId = lossReport.StockLedgerEntryId
            };

            return ApiResponse<LossReportResponse>.SuccessResponse(response, $"Loss Report {lrNumber} successfully generated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Loss Report for discrepancy {Id}", discrepancyId);
            return ApiResponse<LossReportResponse>.FailureResponse($"Failed to create Loss Report: {ex.Message}");
        }
    }

    public async Task<ApiResponse<DiscrepancyResponse>> CreateRtvAsync(int discrepancyId, CreateRtvFromDiscrepancyRequest request)
    {
        try
        {
            var dsc = await _context.Discrepancies
                .Include(d => d.Item)
                .Include(d => d.PurchaseOrder)
                .Include(d => d.GoodsReceipt).ThenInclude(g => g.Items)
                .Include(d => d.NonConformanceReport)
                .FirstOrDefaultAsync(d => d.DiscrepancyId == discrepancyId);

            if (dsc == null)
                return ApiResponse<DiscrepancyResponse>.FailureResponse($"Discrepancy {discrepancyId} not found.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            var rtvNumber = await _documentNumbers.NextAsync(DocumentType.ReturnToVendor, now);

            // Find rejected lot
            InventoryLot? lot = null;
            if (dsc.NonConformanceReport?.LotId.HasValue == true)
            {
                lot = await _context.InventoryLots.FindAsync(dsc.NonConformanceReport.LotId.Value);
            }
            else
            {
                var grnItem = dsc.GoodsReceipt?.Items.FirstOrDefault(i => i.ItemId == dsc.ItemId);
                if (grnItem?.LotId.HasValue == true)
                {
                    lot = await _context.InventoryLots.FindAsync(grnItem.LotId.Value);
                }
            }

            if (lot == null)
            {
                return ApiResponse<DiscrepancyResponse>.FailureResponse("Cannot create Return to Supplier without a physical lot.");
            }

            var rtv = new ReturnToVendor
            {
                RtvNumber = rtvNumber,
                NcrId = dsc.NcrId,
                SupplierId = dsc.PurchaseOrder.SupplierId,
                ItemId = dsc.ItemId,
                LotId = lot.LotId,
                ReturnedQuantity = dsc.DiscrepancyQuantity,
                Reason = request.Reason ?? $"Return arising from Discrepancy {dsc.DiscrepancyNumber}",
                ApprovalRequestNotes = request.Notes,
                Status = RtvStatus.PendingApproval,
                CreatedBy = actor.AuditName,
                CreatedAt = now
            };

            await _posting.ExecuteAsync(async () =>
            {
                _context.ReturnToVendors.Add(rtv);

                dsc.Status = DiscrepancyStatus.Resolved;
                dsc.ResolutionType = "ReturnToSupplier";
                dsc.ReturnToVendor = rtv;
                dsc.ResolvedBy = actor.AuditName;
                dsc.ResolvedAt = now;
                _context.Discrepancies.Update(dsc);

                if (dsc.NonConformanceReport != null)
                {
                    dsc.NonConformanceReport.Disposition = "ReturnToSupplier";
                    dsc.NonConformanceReport.DispositionNotes = request.Notes;
                    dsc.NonConformanceReport.DispositionBy = actor.AuditName;
                    dsc.NonConformanceReport.DispositionAt = now;
                    dsc.NonConformanceReport.Status = NcrStatus.Closed;
                    _context.NonConformanceReports.Update(dsc.NonConformanceReport);
                }

                _audit.Record(nameof(ReturnToVendor), rtvNumber, "Created", "Status", null, "PendingApproval");
                return rtv;
            });

            return await GetDiscrepancyByIdAsync(discrepancyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating RTV for discrepancy {Id}", discrepancyId);
            return ApiResponse<DiscrepancyResponse>.FailureResponse($"Failed to create Return to Supplier: {ex.Message}");
        }
    }

    private static DiscrepancyResponse MapToResponse(Discrepancy d) => new()
    {
        DiscrepancyId = d.DiscrepancyId,
        DiscrepancyNumber = d.DiscrepancyNumber,
        DiscrepancyType = EnumDbValue.ToDbValue(d.DiscrepancyType),
        GrnId = d.GrnId,
        GrnNumber = d.GrnNumber,
        PoId = d.PoId,
        PoNumber = d.PoNumber,
        PrId = d.PurchaseOrder?.PrId,
        PrNumber = d.PurchaseOrder?.PurchaseRequisition?.PrNumber,
        SupplierName = d.PurchaseOrder?.Supplier?.CompanyName,
        DeliveryId = d.DeliveryId,
        DeliveryNumber = d.DeliveryNumber,
        ItemId = d.ItemId,
        ItemName = d.Item?.ItemName ?? $"Item {d.ItemId}",
        OrderedQuantity = d.OrderedQuantity,
        PreviouslyReceivedQty = d.PreviouslyReceivedQty,
        CurrentReceivedQty = d.CurrentReceivedQty,
        DiscrepancyQuantity = d.DiscrepancyQuantity,
        Status = EnumDbValue.ToDbValue(d.Status),
        ResolutionType = d.ResolutionType,
        ResolutionNotes = d.ResolutionNotes,
        LossReportId = d.LossReportId,
        LossReportNumber = d.LossReport?.LossReportNumber,
        RtvId = d.RtvId,
        RtvNumber = d.ReturnToVendor?.RtvNumber,
        NcrId = d.NcrId,
        NcrNumber = d.NonConformanceReport?.NcrNumber,
        ResolvedBy = d.ResolvedBy,
        ResolvedAt = d.ResolvedAt,
        CreatedAt = d.CreatedAt
    };
}
