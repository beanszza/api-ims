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

public class NcrService : INcrService
{
    private readonly ScmDbContext _context;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly IPostingTransaction _posting;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<NcrService> _logger;

    public NcrService(
        ScmDbContext context,
        IDocumentNumberService documentNumbers,
        IPostingTransaction posting,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<NcrService> logger)
    {
        _context = context;
        _documentNumbers = documentNumbers;
        _posting = posting;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<List<NcrResponse>>> GetNcrsAsync(string? status = null)
    {
        try
        {
            var query = _context.NonConformanceReports
                .Include(n => n.Supplier)
                .Include(n => n.Item)
                .Include(n => n.Lot)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (EnumDbValue.TryParse<NcrStatus>(status, out var st))
                    query = query.Where(n => n.Status == st);
            }

            var list = await query.OrderByDescending(n => n.NcrId).ToListAsync();
            return ApiResponse<List<NcrResponse>>.SuccessResponse(list.Select(MapToNcrResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving NCRs");
            return ApiResponse<List<NcrResponse>>.FailureResponse("An error occurred while fetching non-conformance reports.");
        }
    }

    public async Task<ApiResponse<NcrResponse>> GetNcrByIdAsync(int ncrId)
    {
        try
        {
            var ncr = await _context.NonConformanceReports
                .Include(n => n.Supplier)
                .Include(n => n.Item)
                .Include(n => n.Lot)
                .FirstOrDefaultAsync(n => n.NcrId == ncrId);

            if (ncr == null)
                return ApiResponse<NcrResponse>.FailureResponse($"NCR {ncrId} not found.");

            return ApiResponse<NcrResponse>.SuccessResponse(MapToNcrResponse(ncr));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving NCR {NcrId}", ncrId);
            return ApiResponse<NcrResponse>.FailureResponse("An error occurred while fetching NCR.");
        }
    }

    public async Task<ApiResponse<NcrResponse>> CreateNcrAsync(CreateNcrRequest request)
    {
        try
        {
            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            var ncrNumber = await _documentNumbers.NextAsync(DocumentType.NonConformance, now);

            var ncr = new NonConformanceReport
            {
                NcrNumber = ncrNumber,
                InspectionId = request.InspectionId,
                SupplierId = request.SupplierId,
                ItemId = request.ItemId,
                LotId = request.LotId,
                DefectiveQuantity = request.DefectiveQuantity,
                DefectType = request.DefectType.Trim(),
                Severity = request.Severity,
                RootCause = request.RootCause,
                CorrectiveAction = request.CorrectiveAction,
                Status = NcrStatus.Open,
                CreatedBy = actor.AuditName,
                CreatedAt = now
            };

            _context.NonConformanceReports.Add(ncr);
            await _context.SaveChangesAsync();

            _audit.Record(
                nameof(NonConformanceReport),
                ncr.NcrNumber,
                "NcrCreated",
                fieldName: "Status",
                oldValue: null,
                newValue: "Open");

            var reloaded = await _context.NonConformanceReports
                .Include(n => n.Supplier)
                .Include(n => n.Item)
                .Include(n => n.Lot)
                .FirstAsync(n => n.NcrId == ncr.NcrId);

            return ApiResponse<NcrResponse>.SuccessResponse(MapToNcrResponse(reloaded), $"Non-Conformance Report {ncr.NcrNumber} filed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating NCR");
            return ApiResponse<NcrResponse>.FailureResponse($"Failed to create NCR: {ex.Message}");
        }
    }

    public async Task<ApiResponse<NcrResponse>> ResolveNcrAsync(int ncrId, ResolveNcrRequest request)
    {
        try
        {
            var ncr = await _context.NonConformanceReports
                .Include(n => n.Supplier)
                .Include(n => n.Item)
                .Include(n => n.Lot)
                .FirstOrDefaultAsync(n => n.NcrId == ncrId);

            if (ncr == null)
                return ApiResponse<NcrResponse>.FailureResponse($"NCR {ncrId} not found.");

            if (!EnumDbValue.TryParse<NcrStatus>(request.Status, out var newStatus))
                return ApiResponse<NcrResponse>.FailureResponse($"Invalid NCR status '{request.Status}'.");

            var actor = _currentUser.Current;
            var oldStatus = ncr.Status;

            ncr.Status = newStatus;
            if (!string.IsNullOrWhiteSpace(request.CorrectiveAction))
                ncr.CorrectiveAction = request.CorrectiveAction;

            ncr.ResolvedBy = actor.AuditName;
            ncr.ResolvedAt = DateTime.UtcNow;

            _context.NonConformanceReports.Update(ncr);
            await _context.SaveChangesAsync();

            _audit.Record(
                nameof(NonConformanceReport),
                ncr.NcrNumber,
                "NcrResolved",
                fieldName: "Status",
                oldValue: EnumDbValue.ToDbValue(oldStatus),
                newValue: EnumDbValue.ToDbValue(newStatus));

            return ApiResponse<NcrResponse>.SuccessResponse(MapToNcrResponse(ncr), $"NCR {ncr.NcrNumber} status updated to {EnumDbValue.ToDbValue(newStatus)}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving NCR {NcrId}", ncrId);
            return ApiResponse<NcrResponse>.FailureResponse("An error occurred while resolving NCR.");
        }
    }

    public async Task<ApiResponse<List<RtvResponse>>> GetRtvsAsync(string? status = null)
    {
        try
        {
            var query = _context.ReturnToVendors
                .Include(r => r.Supplier)
                .Include(r => r.Item)
                .Include(r => r.Lot)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (EnumDbValue.TryParse<RtvStatus>(status, out var st))
                    query = query.Where(r => r.Status == st);
            }

            var list = await query.OrderByDescending(r => r.RtvId).ToListAsync();
            return ApiResponse<List<RtvResponse>>.SuccessResponse(list.Select(MapToRtvResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving RTVs");
            return ApiResponse<List<RtvResponse>>.FailureResponse("An error occurred while fetching return to vendor records.");
        }
    }

    public async Task<ApiResponse<RtvResponse>> GetRtvByIdAsync(int rtvId)
    {
        try
        {
            var rtv = await _context.ReturnToVendors
                .Include(r => r.Supplier)
                .Include(r => r.Item)
                .Include(r => r.Lot)
                .FirstOrDefaultAsync(r => r.RtvId == rtvId);

            if (rtv == null)
                return ApiResponse<RtvResponse>.FailureResponse($"RTV {rtvId} not found.");

            return ApiResponse<RtvResponse>.SuccessResponse(MapToRtvResponse(rtv));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving RTV {RtvId}", rtvId);
            return ApiResponse<RtvResponse>.FailureResponse("An error occurred while fetching RTV.");
        }
    }

    public async Task<ApiResponse<RtvResponse>> CreateRtvAsync(CreateRtvRequest request)
    {
        try
        {
            var lot = await _context.InventoryLots.FindAsync(request.LotId);
            if (lot == null)
                return ApiResponse<RtvResponse>.FailureResponse($"Lot {request.LotId} not found.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            var rtvNumber = await _documentNumbers.NextAsync(DocumentType.ReturnToVendor, now);

            var rtv = new ReturnToVendor
            {
                RtvNumber = rtvNumber,
                NcrId = request.NcrId,
                SupplierId = request.SupplierId,
                ItemId = request.ItemId,
                LotId = request.LotId,
                ReturnedQuantity = request.ReturnedQuantity,
                Reason = request.Reason.Trim(),
                Status = RtvStatus.PendingDispatch,
                CreatedBy = actor.AuditName,
                CreatedAt = now
            };

            _context.ReturnToVendors.Add(rtv);
            await _context.SaveChangesAsync();

            _audit.Record(
                nameof(ReturnToVendor),
                rtv.RtvNumber,
                "RtvCreated",
                fieldName: "Status",
                oldValue: null,
                newValue: "Pending Dispatch");

            var reloaded = await _context.ReturnToVendors
                .Include(r => r.Supplier)
                .Include(r => r.Item)
                .Include(r => r.Lot)
                .FirstAsync(r => r.RtvId == rtv.RtvId);

            return ApiResponse<RtvResponse>.SuccessResponse(MapToRtvResponse(reloaded), $"RTV {rtv.RtvNumber} created.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating RTV");
            return ApiResponse<RtvResponse>.FailureResponse($"Failed to create RTV: {ex.Message}");
        }
    }

    public async Task<ApiResponse<RtvResponse>> DispatchRtvAsync(int rtvId, DispatchRtvRequest request)
    {
        try
        {
            var rtv = await _context.ReturnToVendors
                .Include(r => r.Supplier)
                .Include(r => r.Item)
                .Include(r => r.Lot)
                .FirstOrDefaultAsync(r => r.RtvId == rtvId);

            if (rtv == null)
                return ApiResponse<RtvResponse>.FailureResponse($"RTV {rtvId} not found.");

            if (rtv.Status != RtvStatus.PendingDispatch)
                return ApiResponse<RtvResponse>.FailureResponse($"RTV is already {EnumDbValue.ToDbValue(rtv.Status)}.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            await _posting.ExecuteAsync(async () =>
            {
                rtv.Status = !string.IsNullOrWhiteSpace(request.CreditNoteNumber) ? RtvStatus.CreditNoteReceived : RtvStatus.Dispatched;
                rtv.DispatchedDate = now;
                rtv.CreditNoteNumber = request.CreditNoteNumber;

                // Update lot status
                if (rtv.Lot != null)
                {
                    rtv.Lot.Status = LotStatus.Returned;
                    rtv.Lot.QuantityRemaining = 0; // Dispatched out of warehouse
                    _context.InventoryLots.Update(rtv.Lot);
                }

                _context.ReturnToVendors.Update(rtv);

                _audit.Record(
                    nameof(ReturnToVendor),
                    rtv.RtvNumber,
                    "RtvDispatched",
                    fieldName: "Status",
                    oldValue: "Pending Dispatch",
                    newValue: EnumDbValue.ToDbValue(rtv.Status));

                return rtv;
            });

            return ApiResponse<RtvResponse>.SuccessResponse(
                MapToRtvResponse(rtv),
                $"RTV {rtv.RtvNumber} dispatched to supplier. Inventory lot marked Returned.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching RTV {RtvId}", rtvId);
            return ApiResponse<RtvResponse>.FailureResponse($"Failed to dispatch RTV: {ex.Message}");
        }
    }

    private static NcrResponse MapToNcrResponse(NonConformanceReport n) => new()
    {
        NcrId = n.NcrId,
        NcrNumber = n.NcrNumber,
        InspectionId = n.InspectionId,
        SupplierId = n.SupplierId,
        SupplierName = n.Supplier?.CompanyName ?? string.Empty,
        ItemId = n.ItemId,
        ItemName = n.Item?.ItemName ?? string.Empty,
        LotId = n.LotId,
        LotCode = n.Lot?.LotCode,
        DefectiveQuantity = n.DefectiveQuantity,
        DefectType = n.DefectType,
        Severity = n.Severity,
        RootCause = n.RootCause,
        CorrectiveAction = n.CorrectiveAction,
        Status = EnumDbValue.ToDbValue(n.Status),
        CreatedBy = n.CreatedBy,
        CreatedAt = n.CreatedAt,
        ResolvedBy = n.ResolvedBy,
        ResolvedAt = n.ResolvedAt
    };

    private static RtvResponse MapToRtvResponse(ReturnToVendor r) => new()
    {
        RtvId = r.RtvId,
        RtvNumber = r.RtvNumber,
        NcrId = r.NcrId,
        SupplierId = r.SupplierId,
        SupplierName = r.Supplier?.CompanyName ?? string.Empty,
        ItemId = r.ItemId,
        ItemName = r.Item?.ItemName ?? string.Empty,
        LotId = r.LotId,
        LotCode = r.Lot?.LotCode ?? string.Empty,
        ReturnedQuantity = r.ReturnedQuantity,
        Reason = r.Reason,
        Status = EnumDbValue.ToDbValue(r.Status),
        DispatchedDate = r.DispatchedDate,
        CreditNoteNumber = r.CreditNoteNumber,
        CreatedBy = r.CreatedBy,
        CreatedAt = r.CreatedAt
    };
}
