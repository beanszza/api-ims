using Api.Contracts.Production;
using Microsoft.AspNetCore.Http;

namespace Applications.Interfaces;

public interface IProductionService
{
    Task<ProductionBatchResponse> CreateBatchAsync(CreateProductionBatchRequest request);
    Task<IEnumerable<ProductionBatchResponse>> GetAllBatchesAsync();
    Task<ProductionBatchResponse> GetBatchByIdAsync(int batchId);
    Task<ProductionBatchResponse> ApproveBatchAsync(int batchId);
    Task<ProductionBatchResponse> RejectBatchAsync(int batchId, string reason);
    Task<ProductionBatchResponse> CancelBatchAsync(int batchId);
    Task<ProductionBatchResponse> UpdateStageAsync(int batchId, UpdateStageRequest request);
    Task<ProductionBatchResponse> SubmitQaAsync(int batchId, UpdateQaNotesRequest request);
    Task<ProductionBatchResponse> UpdateQaApprovalAsync(int batchId, UpdateQaApprovalRequest request);
    Task<ProductionBatchResponse> UploadImageAsync(int batchId, IFormFile file);
    Task<DashboardSummaryResponse> GetDashboardSummaryAsync();
    Task<ProductionBatchResponse> AddBatchToInventoryAsync(int batchId);
    Task<IEnumerable<LowStockAlertResponse>> GetLowStockAlertsAsync();
}
