using System.Threading.Tasks;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IValuationService
{
    Task<ApiResponse<InventoryValuationReportResponse>> GetValuationReportAsync(int? locationId = null, int? categoryId = null);
}
