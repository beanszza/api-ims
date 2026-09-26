using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IFinishedProductService
{
    Task<ApiResponse<IEnumerable<FinishedProductResponse>>> GetAllFinishedProductsAsync();
    Task<ApiResponse<FinishedProductResponse>> CreateFinishedProductAsync(CreateFinishedProductRequest request);
    Task<ApiResponse<FinishedProductResponse>> UpdateFinishedProductAsync(int id, UpdateFinishedProductRequest request);
    Task<ApiResponse<bool>> DeleteFinishedProductAsync(int id);
}
