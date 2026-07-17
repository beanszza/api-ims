using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Api.Contracts.Requests;
using api_scm.Api.Contracts.Responses;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IReportService
{
    Task<ApiResponse<IEnumerable<InventoryReportDto>>> GetInventoryReportAsync(ReportFilterDto filter);
    Task<ApiResponse<IEnumerable<ProcurementReportDto>>> GetProcurementReportAsync(ReportFilterDto filter);
    Task<ApiResponse<IEnumerable<ProductionQualityReportDto>>> GetProductionReportAsync(ReportFilterDto filter);
    Task<ApiResponse<IEnumerable<SupplierPerformanceReportDto>>> GetSupplierReportAsync(ReportFilterDto filter);
    Task<ApiResponse<IEnumerable<DistributionReportDto>>> GetDistributionReportAsync(ReportFilterDto filter);
}
