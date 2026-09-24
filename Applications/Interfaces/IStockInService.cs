using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IStockInService
{
    Task<ApiResponse<List<StockInResponse>>> GetStockInsAsync(string? status = null, int? grnId = null);
    Task<ApiResponse<StockInResponse>> GetStockInByIdAsync(int id);
    Task<ApiResponse<StockInResponse>> CreateStockInAsync(CreateStockInRequest request);
    Task<ApiResponse<StockInResponse>> SubmitForApprovalAsync(int id);
    Task<ApiResponse<StockInResponse>> ApproveStockInAsync(int id, ApproveStockInRequest request);
    Task<ApiResponse<StockInResponse>> RejectStockInAsync(int id, RejectStockInRequest request);
    Task<ApiResponse<StockInResponse>> CommitStockInAsync(int id, CommitStockInRequest? request = null);
}
