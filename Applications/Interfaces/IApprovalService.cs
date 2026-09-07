using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IApprovalService
{
    Task<ApiResponse<List<ApprovalRequestResponse>>> GetPendingApprovalsAsync();
    Task<ApiResponse<List<ApprovalRequestResponse>>> GetApprovalHistoryAsync(string? entityType = null, int? entityId = null);
    Task<ApiResponse<ApprovalRequestResponse>> GetApprovalByIdAsync(int approvalRequestId);
    Task<ApiResponse<ApprovalRequestResponse>> CreateApprovalRequestAsync(CreateApprovalRequest request);
    Task<ApiResponse<ApprovalRequestResponse>> ActOnApprovalAsync(int approvalRequestId, ActOnApprovalRequest request);
}
