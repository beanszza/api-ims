using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IBranchDistributionService
{
    Task<ApiResponse<List<BranchRequestResponse>>> GetBranchRequestsAsync(int? branchId = null, string? status = null);
    Task<ApiResponse<BranchRequestResponse>> GetBranchRequestByIdAsync(int requestId);
    Task<ApiResponse<BranchRequestResponse>> CreateBranchRequestAsync(CreateBranchRequestDto request);
    Task<ApiResponse<StockTransferResponse>> ApproveAndGenerateShipmentAsync(int requestId, DispatchShipmentRequest? dispatchDetails = null);
    Task<ApiResponse<BranchRequestResponse>> RejectBranchRequestAsync(int requestId, string reason);

    Task<ApiResponse<List<BranchReturnResponse>>> GetBranchReturnsAsync(int? branchId = null);
    Task<ApiResponse<BranchReturnResponse>> GetBranchReturnByIdAsync(int returnId);
    Task<ApiResponse<BranchReturnResponse>> CreateBranchReturnAsync(CreateBranchReturnRequest request);
    Task<ApiResponse<BranchReturnResponse>> ReceiveBranchReturnAsync(int returnId);
}
