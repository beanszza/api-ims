using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Api.Contracts.Requests;
using api_scm.Api.Contracts.Responses;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IReportService
{
    Task<ApiResponse<InventoryReportResponseDto>> GetInventoryReportAsync(ReportFilterDto filter);
    Task<ApiResponse<ProcurementReportResponseDto>> GetProcurementReportAsync(ReportFilterDto filter);
    Task<ApiResponse<ProductionQualityReportResponseDto>> GetProductionReportAsync(ReportFilterDto filter);
    Task<ApiResponse<SupplierPerformanceReportResponseDto>> GetSupplierReportAsync(ReportFilterDto filter);
    Task<ApiResponse<DistributionReportResponseDto>> GetDistributionReportAsync(ReportFilterDto filter);
    Task<ApiResponse<SupplyListReportResponseDto>> GetSupplyListReportAsync(ReportFilterDto filter);
    Task<ApiResponse<RecipeReportResponseDto>> GetRecipeReportAsync(ReportFilterDto filter);
}
