using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IPutAwayService
{
    Task<ApiResponse<List<PutAwayResponse>>> GetPutAwaysAsync(string? status = null, int? grnId = null);
    Task<ApiResponse<PutAwayResponse>> GetPutAwayByIdAsync(int id);
    Task<ApiResponse<PutAwayResponse>> CompletePutAwayAsync(int id, CompletePutAwayRequest request);
    Task<ApiResponse<List<PutAwayResponse>>> BatchCompletePutAwayAsync(BatchCompletePutAwayRequest request);
}
