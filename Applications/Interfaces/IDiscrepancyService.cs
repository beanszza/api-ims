using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IDiscrepancyService
{
    Task<ApiResponse<List<DiscrepancyResponse>>> GetDiscrepanciesAsync(string? type = null, string? status = null, int? grnId = null, int? poId = null);
    Task<ApiResponse<DiscrepancyResponse>> GetDiscrepancyByIdAsync(int id);
    Task<ApiResponse<DiscrepancyResponse>> ResolveAsync(int id, ResolveDiscrepancyRequest request);
    Task<ApiResponse<LossReportResponse>> CreateLossReportAsync(int discrepancyId, CreateLossReportRequest request);
    Task<ApiResponse<DiscrepancyResponse>> CreateRtvAsync(int discrepancyId, CreateRtvFromDiscrepancyRequest request);
}
