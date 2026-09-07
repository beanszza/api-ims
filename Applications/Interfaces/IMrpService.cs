using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IMrpService
{
    Task<ApiResponse<MrpPlanResponse>> GeneratePlanAsync(GenerateMrpPlanRequest request);
}
