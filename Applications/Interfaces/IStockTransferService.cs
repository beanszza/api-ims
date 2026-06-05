using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Domains.Entities;

namespace Applications.Interfaces;

public interface IStockTransferService
{
    Task<ApiResponse<IEnumerable<StockTransferResponse>>> GetAllTransfersAsync();
    Task<ApiResponse<StockTransferResponse>> CreateTransferAsync(CreateStockTransferRequest request, int userId);
    Task<ApiResponse<StockTransferResponse>> UpdateTransferStatusAsync(int transferId, UpdateStockTransferStatusRequest request, int userId);
}
