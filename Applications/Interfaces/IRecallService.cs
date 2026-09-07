using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Domains.Entities;

namespace Applications.Interfaces;

public interface IRecallService
{
    Task<ApiResponse<RecallSimulationResponse>> SimulateRecallAsync(SimulateRecallRequest request);
    Task<ApiResponse<List<RecallRecord>>> GetRecallHistoryAsync();
}
