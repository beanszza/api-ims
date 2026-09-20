using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface ISupplierScorecardService
{
    Task<ApiResponse<SupplierScorecardResponse>> GetSupplierScorecardAsync(int supplierId);
    Task<ApiResponse<List<SupplierScorecardResponse>>> GetAllSupplierScorecardsAsync();
}
