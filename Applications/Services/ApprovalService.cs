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

public class ApprovalService : IApprovalService
{
    private readonly ScmDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditTrail _audit;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(
        ScmDbContext context,
        ICurrentUserService currentUser,
        IAuditTrail audit,
        ILogger<ApprovalService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<List<ApprovalRequestResponse>>> GetPendingApprovalsAsync()
    {
        try
        {
            var pending = await _context.ApprovalRequests
                .Where(a => a.Status == ApprovalStatus.Pending)
                .OrderByDescending(a => a.RequestedAt)
                .ToListAsync();

            return ApiResponse<List<ApprovalRequestResponse>>.SuccessResponse(pending.Select(MapToResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending approval requests");
            return ApiResponse<List<ApprovalRequestResponse>>.FailureResponse("An error occurred while fetching pending approvals.");
        }
    }

    public async Task<ApiResponse<List<ApprovalRequestResponse>>> GetApprovalHistoryAsync(string? entityType = null, int? entityId = null)
    {
        try
        {
            var query = _context.ApprovalRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(entityType))
                query = query.Where(a => a.EntityType.ToLower() == entityType.ToLower());

            if (entityId.HasValue)
                query = query.Where(a => a.EntityId == entityId.Value);

            var list = await query.OrderByDescending(a => a.RequestedAt).ToListAsync();
            return ApiResponse<List<ApprovalRequestResponse>>.SuccessResponse(list.Select(MapToResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approval history");
            return ApiResponse<List<ApprovalRequestResponse>>.FailureResponse("An error occurred while fetching approval history.");
        }
    }

    public async Task<ApiResponse<ApprovalRequestResponse>> GetApprovalByIdAsync(int approvalRequestId)
    {
        try
        {
            var approval = await _context.ApprovalRequests.FindAsync(approvalRequestId);
            if (approval == null)
                return ApiResponse<ApprovalRequestResponse>.FailureResponse($"Approval request {approvalRequestId} not found.");

            return ApiResponse<ApprovalRequestResponse>.SuccessResponse(MapToResponse(approval));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approval request {Id}", approvalRequestId);
            return ApiResponse<ApprovalRequestResponse>.FailureResponse("An error occurred while fetching approval request.");
        }
    }

    public async Task<ApiResponse<ApprovalRequestResponse>> CreateApprovalRequestAsync(CreateApprovalRequest request)
    {
        try
        {
            var actor = _currentUser.Current;

            var approval = new ApprovalRequest
            {
                EntityType = request.EntityType.Trim(),
                EntityId = request.EntityId,
                DocumentNumber = request.DocumentNumber.Trim(),
                Amount = request.Amount,
                Status = ApprovalStatus.Pending,
                Reason = request.Reason.Trim(),
                RequestedBy = actor.AuditName,
                RequestedAt = DateTime.UtcNow
            };

            _context.ApprovalRequests.Add(approval);
            await _context.SaveChangesAsync();

            _audit.Record(
                approval.EntityType,
                approval.DocumentNumber,
                "ApprovalRequested",
                fieldName: "Status",
                oldValue: null,
                newValue: "Pending");

            return ApiResponse<ApprovalRequestResponse>.SuccessResponse(MapToResponse(approval), "Approval request created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating approval request for {EntityType} {EntityId}", request.EntityType, request.EntityId);
            return ApiResponse<ApprovalRequestResponse>.FailureResponse("An error occurred while submitting approval request.");
        }
    }

    public async Task<ApiResponse<ApprovalRequestResponse>> ActOnApprovalAsync(int approvalRequestId, ActOnApprovalRequest request)
    {
        try
        {
            var approval = await _context.ApprovalRequests.FindAsync(approvalRequestId);
            if (approval == null)
                return ApiResponse<ApprovalRequestResponse>.FailureResponse($"Approval request {approvalRequestId} not found.");

            if (approval.Status != ApprovalStatus.Pending)
                return ApiResponse<ApprovalRequestResponse>.FailureResponse($"Approval request is already {EnumDbValue.ToDbValue(approval.Status)}.");

            var actor = _currentUser.Current;
            var oldStatus = approval.Status;
            var newStatus = request.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;

            approval.Status = newStatus;
            approval.ApproverId = actor.UserId;
            approval.ApproverName = actor.AuditName;
            approval.ActionAt = DateTime.UtcNow;
            approval.Comments = request.Comments;

            _context.ApprovalRequests.Update(approval);
            await _context.SaveChangesAsync();

            _audit.Record(
                approval.EntityType,
                approval.DocumentNumber,
                request.Approve ? "ApprovalGranted" : "ApprovalRejected",
                fieldName: "Status",
                oldValue: EnumDbValue.ToDbValue(oldStatus),
                newValue: EnumDbValue.ToDbValue(newStatus));

            return ApiResponse<ApprovalRequestResponse>.SuccessResponse(
                MapToResponse(approval),
                $"Approval request was {EnumDbValue.ToDbValue(newStatus).ToLower()} successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acting on approval request {Id}", approvalRequestId);
            return ApiResponse<ApprovalRequestResponse>.FailureResponse("An error occurred while recording approval decision.");
        }
    }

    private static ApprovalRequestResponse MapToResponse(ApprovalRequest a) => new()
    {
        ApprovalRequestId = a.ApprovalRequestId,
        EntityType = a.EntityType,
        EntityId = a.EntityId,
        DocumentNumber = a.DocumentNumber,
        Amount = a.Amount,
        Status = EnumDbValue.ToDbValue(a.Status),
        Reason = a.Reason,
        RequestedBy = a.RequestedBy,
        RequestedAt = a.RequestedAt,
        ApproverId = a.ApproverId,
        ApproverName = a.ApproverName,
        ActionAt = a.ActionAt,
        Comments = a.Comments
    };
}
