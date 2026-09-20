using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Domains.Entities;

namespace Applications.Interfaces;

public interface IStockTransferService
{
    Task<ApiResponse<PagedData<StockTransferResponse>>> GetAllTransfersAsync(string? status = null, string? search = null, int page = 1, int pageSize = 10);
    // The acting user comes from ICurrentUserService, not from the caller.
    Task<ApiResponse<StockTransferResponse>> CreateTransferAsync(CreateStockTransferRequest request);
    Task<ApiResponse<StockTransferResponse>> UpdateTransferAsync(int transferId, UpdateStockTransferRequest request);
    Task<ApiResponse<StockTransferResponse>> UpdateTransferStatusAsync(int transferId, UpdateStockTransferStatusRequest request);
    Task<ApiResponse<TransferDashboardResponse>> GetTransferDashboardSummaryAsync();
    Task<ApiResponse<PagedData<TransferHistoryResponse>>> GetTransferHistoryAsync(string? status = null, DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 10);
}
