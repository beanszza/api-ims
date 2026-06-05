using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IFinishedProductService
{
    Task<ApiResponse<IEnumerable<FinishedProductResponse>>> GetAllFinishedProductsAsync();
}
