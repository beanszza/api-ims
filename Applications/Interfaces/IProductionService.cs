using Api.Contracts.Production;

namespace Applications.Interfaces;

public interface IProductionService
{
    Task<ProductionBatchResponse> CreateBatchAsync(CreateProductionBatchRequest request);
    Task<IEnumerable<ProductionBatchResponse>> GetAllBatchesAsync();
    Task<ProductionBatchResponse> UpdateStageAsync(int batchId, UpdateStageRequest request);
    Task<ProductionBatchResponse> SubmitQaAsync(int batchId, UpdateQaNotesRequest request);
    Task<ProductionBatchResponse> UpdateQaApprovalAsync(int batchId, UpdateQaApprovalRequest request);
    Task<ProductionBatchResponse> UploadImageAsync(int batchId, string imageUrl);
    Task<DashboardSummaryResponse> GetDashboardSummaryAsync();
    Task<ProductionBatchResponse> AddBatchToInventoryAsync(int batchId);
    Task<IEnumerable<LowStockAlertResponse>> GetLowStockAlertsAsync();
}
