using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface ICycleCountService
{
    Task<ApiResponse<List<CycleCountResponse>>> GetCycleCountsAsync(int? locationId = null);
    Task<ApiResponse<CycleCountResponse>> GetCycleCountByIdAsync(int countId);
    Task<ApiResponse<CycleCountResponse>> CreateCycleCountAsync(CreateCycleCountRequest request);
    Task<ApiResponse<CycleCountResponse>> ReconcileCycleCountAsync(int countId, ReconcileCycleCountRequest? request = null);
    Task<ApiResponse<CycleCountResponse>> CancelCycleCountAsync(int countId, string reason);
}
