using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface INcrService
{
    Task<ApiResponse<List<NcrResponse>>> GetNcrsAsync(string? status = null);
    Task<ApiResponse<NcrResponse>> GetNcrByIdAsync(int ncrId);
    Task<ApiResponse<NcrResponse>> CreateNcrAsync(CreateNcrRequest request);
    Task<ApiResponse<NcrResponse>> ResolveNcrAsync(int ncrId, ResolveNcrRequest request);

    Task<ApiResponse<List<RtvResponse>>> GetRtvsAsync(string? status = null);
    Task<ApiResponse<RtvResponse>> GetRtvByIdAsync(int rtvId);
    Task<ApiResponse<RtvResponse>> CreateRtvAsync(CreateRtvRequest request);
    Task<ApiResponse<RtvResponse>> DispatchRtvAsync(int rtvId, DispatchRtvRequest request);
}
