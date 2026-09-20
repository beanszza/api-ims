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

public class BranchDistributionService : IBranchDistributionService
{
    private readonly ScmDbContext _context;
    private readonly IDocumentNumberService _documentNumbers;
    private readonly IStockTransferService _transferService;
    private readonly ILocationResolver _locations;
    private readonly IPostingTransaction _posting;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<BranchDistributionService> _logger;

    public BranchDistributionService(
        ScmDbContext context,
        IDocumentNumberService documentNumbers,
        IStockTransferService transferService,
        ILocationResolver locations,
        IPostingTransaction posting,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<BranchDistributionService> logger)
    {
        _context = context;
        _documentNumbers = documentNumbers;
        _transferService = transferService;
        _locations = locations;
        _posting = posting;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<List<BranchRequestResponse>>> GetBranchRequestsAsync(int? branchId = null, string? status = null)
    {
        try
        {
            var query = _context.BranchRequests
                .Include(r => r.Branch)
                .Include(r => r.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Item)
                .AsQueryable();

            if (branchId.HasValue && branchId.Value > 0)
                query = query.Where(r => r.BranchId == branchId.Value);

            if (!string.IsNullOrWhiteSpace(status) && EnumDbValue.TryParse<BranchRequestStatus>(status, out var parsedStatus))
                query = query.Where(r => r.Status == parsedStatus);

            var list = await query.OrderByDescending(r => r.BranchRequestId).ToListAsync();
            return ApiResponse<List<BranchRequestResponse>>.SuccessResponse(list.Select(MapToResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving branch requests");
            return ApiResponse<List<BranchRequestResponse>>.FailureResponse("An error occurred while fetching branch requests.");
        }
    }

    public async Task<ApiResponse<BranchRequestResponse>> GetBranchRequestByIdAsync(int requestId)
    {
        try
        {
            var req = await _context.BranchRequests
                .Include(r => r.Branch)
                .Include(r => r.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Item)
                .FirstOrDefaultAsync(r => r.BranchRequestId == requestId);

            if (req == null)
                return ApiResponse<BranchRequestResponse>.FailureResponse($"Branch request {requestId} not found.");

            return ApiResponse<BranchRequestResponse>.SuccessResponse(MapToResponse(req));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving branch request {RequestId}", requestId);
            return ApiResponse<BranchRequestResponse>.FailureResponse("An error occurred while fetching branch request.");
        }
    }

    public async Task<ApiResponse<BranchRequestResponse>> CreateBranchRequestAsync(CreateBranchRequestDto request)
    {
        try
        {
            if (request.Items == null || !request.Items.Any())
                return ApiResponse<BranchRequestResponse>.FailureResponse("Request must contain at least one item.");

            var branch = await _context.Locations.FindAsync(request.BranchId);
            if (branch == null)
                return ApiResponse<BranchRequestResponse>.FailureResponse($"Branch location {request.BranchId} not found.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;
            var reqNumber = await _documentNumbers.NextAsync(DocumentType.BranchRequest, now);

            var branchRequest = new BranchRequest
            {
                RequestNumber = reqNumber,
                BranchId = request.BranchId,
                RequestDate = now,
                RequiredDate = request.RequiredDate,
                Status = BranchRequestStatus.Pending,
                RequestedBy = actor.AuditName,
                Notes = request.Notes,
                Items = request.Items.Select(i => new BranchRequestItem
                {
                    ProductId = i.ProductId,
                    RequestedQuantity = i.RequestedQuantity,
                    ApprovedQuantity = i.RequestedQuantity,
                    DispatchedQuantity = 0,
                    ReceivedQuantity = 0,
                    Notes = i.Notes
                }).ToList()
            };

            _context.BranchRequests.Add(branchRequest);
            await _context.SaveChangesAsync();

            _audit.Record(
                nameof(BranchRequest),
                branchRequest.RequestNumber,
                "BranchRequestCreated",
                fieldName: "Status",
                oldValue: null,
                newValue: "Pending");

            var reloaded = await _context.BranchRequests
                .Include(r => r.Branch)
                .Include(r => r.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Item)
                .FirstAsync(r => r.BranchRequestId == branchRequest.BranchRequestId);

            return ApiResponse<BranchRequestResponse>.SuccessResponse(
                MapToResponse(reloaded),
                $"Branch request {branchRequest.RequestNumber} submitted successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating branch request");
            return ApiResponse<BranchRequestResponse>.FailureResponse($"Failed to create branch request: {ex.Message}");
        }
    }

    public async Task<ApiResponse<StockTransferResponse>> ApproveAndGenerateShipmentAsync(
        int requestId, DispatchShipmentRequest? dispatchDetails = null)
    {
        try
        {
            var req = await _context.BranchRequests
                .Include(r => r.Branch)
                .Include(r => r.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Item)
                .FirstOrDefaultAsync(r => r.BranchRequestId == requestId);

            if (req == null)
                return ApiResponse<StockTransferResponse>.FailureResponse($"Branch request {requestId} not found.");

            if (req.Status != BranchRequestStatus.Pending)
                return ApiResponse<StockTransferResponse>.FailureResponse($"Branch request is currently {EnumDbValue.ToDbValue(req.Status)} and cannot be approved.");

            var fgLocation = await _locations.RequireSystemLocationAsync(LocationType.FinishedGoods);
            var firstItem = req.Items.FirstOrDefault();

            if (firstItem == null)
                return ApiResponse<StockTransferResponse>.FailureResponse("Branch request has no items.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;

            req.Status = BranchRequestStatus.Approved;
            req.ApprovedBy = actor.AuditName;

            var transferNumber = await _documentNumbers.NextAsync(DocumentType.Shipment, now);

            var transfer = new StockTransfer
            {
                TransferNumber = transferNumber,
                BranchRequestId = req.BranchRequestId,
                ProductId = firstItem.ProductId,
                SourceLocationId = fgLocation.LocationId,
                DestLocationId = req.BranchId,
                TransferQuantity = firstItem.ApprovedQuantity,
                Status = ShipmentStatus.Pending,
                DriverName = dispatchDetails?.DriverName,
                VehiclePlate = dispatchDetails?.VehiclePlate,
                TransferDate = now
            };

            _context.StockTransfers.Add(transfer);
            await _context.SaveChangesAsync();

            _audit.Record(
                nameof(BranchRequest),
                req.RequestNumber,
                "BranchRequestApproved",
                fieldName: "Status",
                oldValue: "Pending",
                newValue: "Approved");

            return ApiResponse<StockTransferResponse>.SuccessResponse(new StockTransferResponse
            {
                TransferId = transfer.TransferId,
                TransferNumber = transfer.TransferNumber,
                BranchRequestId = transfer.BranchRequestId,
                ProductId = transfer.ProductId,
                ProductName = firstItem.Product?.Item?.ItemName ?? $"Product {firstItem.ProductId}",
                SourceLocationId = transfer.SourceLocationId,
                SourceLocationName = fgLocation.LocationName,
                DestLocationId = transfer.DestLocationId,
                DestLocationName = req.Branch.LocationName,
                TransferQuantity = transfer.TransferQuantity,
                Status = EnumDbValue.ToDbValue(transfer.Status),
                DriverName = transfer.DriverName,
                VehiclePlate = transfer.VehiclePlate,
                TransferDate = transfer.TransferDate
            }, $"Branch request approved. Shipment {transfer.TransferNumber} created for dispatch.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving branch request {RequestId}", requestId);
            return ApiResponse<StockTransferResponse>.FailureResponse($"Failed to approve branch request: {ex.Message}");
        }
    }

    public async Task<ApiResponse<BranchRequestResponse>> RejectBranchRequestAsync(int requestId, string reason)
    {
        try
        {
            var req = await _context.BranchRequests
                .Include(r => r.Branch)
                .Include(r => r.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Item)
                .FirstOrDefaultAsync(r => r.BranchRequestId == requestId);

            if (req == null)
                return ApiResponse<BranchRequestResponse>.FailureResponse($"Branch request {requestId} not found.");

            if (req.Status != BranchRequestStatus.Pending)
                return ApiResponse<BranchRequestResponse>.FailureResponse($"Branch request is currently {EnumDbValue.ToDbValue(req.Status)} and cannot be rejected.");

            var actor = _currentUser.Current;
            req.Status = BranchRequestStatus.Rejected;
            req.Notes = string.IsNullOrWhiteSpace(req.Notes) ? $"Rejection reason: {reason}" : $"{req.Notes} | Rejection: {reason}";

            await _context.SaveChangesAsync();

            _audit.Record(
                nameof(BranchRequest),
                req.RequestNumber,
                "BranchRequestRejected",
                fieldName: "Status",
                oldValue: "Pending",
                newValue: "Rejected");

            return ApiResponse<BranchRequestResponse>.SuccessResponse(
                MapToResponse(req),
                $"Branch request {req.RequestNumber} rejected.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting branch request {RequestId}", requestId);
            return ApiResponse<BranchRequestResponse>.FailureResponse($"Failed to reject branch request: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<BranchReturnResponse>>> GetBranchReturnsAsync(int? branchId = null)
    {
        try
        {
            var query = _context.BranchReturns
                .Include(r => r.Branch)
                .Include(r => r.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Item)
                .Include(r => r.Items).ThenInclude(i => i.Lot)
                .AsQueryable();

            if (branchId.HasValue && branchId.Value > 0)
                query = query.Where(r => r.BranchId == branchId.Value);

            var list = await query.OrderByDescending(r => r.BranchReturnId).ToListAsync();
            return ApiResponse<List<BranchReturnResponse>>.SuccessResponse(list.Select(MapToReturnResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving branch returns");
            return ApiResponse<List<BranchReturnResponse>>.FailureResponse("An error occurred while fetching branch returns.");
        }
    }

    public async Task<ApiResponse<BranchReturnResponse>> GetBranchReturnByIdAsync(int returnId)
    {
        try
        {
            var ret = await _context.BranchReturns
                .Include(r => r.Branch)
                .Include(r => r.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Item)
                .Include(r => r.Items).ThenInclude(i => i.Lot)
                .FirstOrDefaultAsync(r => r.BranchReturnId == returnId);

            if (ret == null)
                return ApiResponse<BranchReturnResponse>.FailureResponse($"Branch return {returnId} not found.");

            return ApiResponse<BranchReturnResponse>.SuccessResponse(MapToReturnResponse(ret));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving branch return {ReturnId}", returnId);
            return ApiResponse<BranchReturnResponse>.FailureResponse("An error occurred while fetching branch return.");
        }
    }

    public async Task<ApiResponse<BranchReturnResponse>> CreateBranchReturnAsync(CreateBranchReturnRequest request)
    {
        try
        {
            if (request.Items == null || !request.Items.Any())
                return ApiResponse<BranchReturnResponse>.FailureResponse("Return must contain at least one item.");

            var branch = await _context.Locations.FindAsync(request.BranchId);
            if (branch == null)
                return ApiResponse<BranchReturnResponse>.FailureResponse($"Branch location {request.BranchId} not found.");

            var actor = _currentUser.Current;
            var now = DateTime.UtcNow;
            var returnNumber = await _documentNumbers.NextAsync(DocumentType.BranchReturn, now);

            var branchReturn = new BranchReturn
            {
                ReturnNumber = returnNumber,
                BranchId = request.BranchId,
                ReturnDate = now,
                Reason = request.Reason.Trim(),
                Status = "Pending",
                ReturnedBy = actor.AuditName,
                Notes = request.Notes,
                Items = request.Items.Select(i => new BranchReturnItem
                {
                    ProductId = i.ProductId,
                    LotId = i.LotId,
                    ReturnedQuantity = i.ReturnedQuantity,
                    DefectCondition = i.DefectCondition,
                    Notes = i.Notes
                }).ToList()
            };

            _context.BranchReturns.Add(branchReturn);
            await _context.SaveChangesAsync();

            _audit.Record(
                nameof(BranchReturn),
                branchReturn.ReturnNumber,
                "BranchReturnCreated",
                fieldName: "Status",
                oldValue: null,
                newValue: "Pending");

            var reloaded = await _context.BranchReturns
                .Include(r => r.Branch)
                .Include(r => r.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Item)
                .Include(r => r.Items).ThenInclude(i => i.Lot)
                .FirstAsync(r => r.BranchReturnId == branchReturn.BranchReturnId);

            return ApiResponse<BranchReturnResponse>.SuccessResponse(
                MapToReturnResponse(reloaded),
                $"Branch return {branchReturn.ReturnNumber} recorded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating branch return");
            return ApiResponse<BranchReturnResponse>.FailureResponse($"Failed to create branch return: {ex.Message}");
        }
    }

    public async Task<ApiResponse<BranchReturnResponse>> ReceiveBranchReturnAsync(int returnId)
    {
        try
        {
            var ret = await _context.BranchReturns
                .Include(r => r.Branch)
                .Include(r => r.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Item)
                .Include(r => r.Items).ThenInclude(i => i.Lot)
                .FirstOrDefaultAsync(r => r.BranchReturnId == returnId);

            if (ret == null)
                return ApiResponse<BranchReturnResponse>.FailureResponse($"Branch return {returnId} not found.");

            if (ret.Status != "Pending")
                return ApiResponse<BranchReturnResponse>.FailureResponse($"Branch return is currently {ret.Status} and cannot be received.");

            var actor = _currentUser.Current;
            ret.Status = "ReceivedAtWarehouse";
            ret.AuthorizedBy = actor.AuditName;

            await _context.SaveChangesAsync();

            _audit.Record(
                nameof(BranchReturn),
                ret.ReturnNumber,
                "BranchReturnReceived",
                fieldName: "Status",
                oldValue: "Pending",
                newValue: "ReceivedAtWarehouse");

            return ApiResponse<BranchReturnResponse>.SuccessResponse(
                MapToReturnResponse(ret),
                $"Branch return {ret.ReturnNumber} received at commissary warehouse.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving branch return {ReturnId}", returnId);
            return ApiResponse<BranchReturnResponse>.FailureResponse($"Failed to receive branch return: {ex.Message}");
        }
    }

    private static BranchRequestResponse MapToResponse(BranchRequest r) => new()
    {
        BranchRequestId = r.BranchRequestId,
        RequestNumber = r.RequestNumber,
        BranchId = r.BranchId,
        BranchName = r.Branch?.LocationName ?? $"Location {r.BranchId}",
        RequestDate = r.RequestDate,
        RequiredDate = r.RequiredDate,
        Status = EnumDbValue.ToDbValue(r.Status),
        RequestedBy = r.RequestedBy,
        ApprovedBy = r.ApprovedBy,
        Notes = r.Notes,
        Items = r.Items.Select(i => new BranchRequestItemResponse
        {
            BranchRequestItemId = i.BranchRequestItemId,
            ProductId = i.ProductId,
            ProductName = i.Product?.Item?.ItemName ?? $"Product {i.ProductId}",
            RequestedQuantity = i.RequestedQuantity,
            ApprovedQuantity = i.ApprovedQuantity,
            DispatchedQuantity = i.DispatchedQuantity,
            ReceivedQuantity = i.ReceivedQuantity,
            Notes = i.Notes
        }).ToList()
    };

    private static BranchReturnResponse MapToReturnResponse(BranchReturn r) => new()
    {
        BranchReturnId = r.BranchReturnId,
        ReturnNumber = r.ReturnNumber,
        BranchId = r.BranchId,
        BranchName = r.Branch?.LocationName ?? $"Location {r.BranchId}",
        ReturnDate = r.ReturnDate,
        Reason = r.Reason,
        Status = r.Status,
        ReturnedBy = r.ReturnedBy,
        AuthorizedBy = r.AuthorizedBy,
        Notes = r.Notes,
        Items = r.Items.Select(i => new BranchReturnItemResponse
        {
            BranchReturnItemId = i.BranchReturnItemId,
            ProductId = i.ProductId,
            ProductName = i.Product?.Item?.ItemName ?? $"Product {i.ProductId}",
            LotId = i.LotId,
            LotCode = i.Lot?.LotCode,
            ReturnedQuantity = i.ReturnedQuantity,
            DefectCondition = i.DefectCondition,
            Notes = i.Notes
        }).ToList()
    };
}
