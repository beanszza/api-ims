using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IDisposalService
{
    Task<ApiResponse<ExpirySweepResponse>> PerformExpirySweepAsync();
    Task<ApiResponse<List<DisposalRecordResponse>>> GetDisposalRecordsAsync();
    Task<ApiResponse<DisposalRecordResponse>> GetDisposalRecordByIdAsync(int disposalId);
    Task<ApiResponse<DisposalRecordResponse>> CreateDisposalRecordAsync(CreateDisposalRequest request);
}
