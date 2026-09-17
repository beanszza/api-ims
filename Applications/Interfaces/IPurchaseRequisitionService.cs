using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IPurchaseRequisitionService
{
    Task<ApiResponse<List<PurchaseRequisitionResponse>>> GetPurchaseRequisitionsAsync(string? status = null);
    Task<ApiResponse<PurchaseRequisitionResponse>> GetPurchaseRequisitionByIdAsync(int prId);
    Task<ApiResponse<PurchaseRequisitionResponse>> CreatePurchaseRequisitionAsync(CreatePurchaseRequisitionRequest request);
    Task<ApiResponse<PurchaseRequisitionResponse>> UpdatePurchaseRequisitionAsync(int prId, UpdatePurchaseRequisitionRequest request);
    Task<ApiResponse<PurchaseRequisitionResponse>> UpdateStatusAsync(int prId, UpdatePurchaseRequisitionStatusRequest request);
    Task<ApiResponse<PrFanOutResultResponse>> FanOutToPurchaseOrdersAsync(int prId);
}

