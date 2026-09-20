using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IQualityInspectionService
{
    Task<ApiResponse<List<QualityInspectionResponse>>> GetInspectionsAsync(string? inspectionType = null);
    Task<ApiResponse<QualityInspectionResponse>> GetInspectionByIdAsync(int inspectionId);
    Task<ApiResponse<QualityInspectionResponse>> InspectIncomingGoodsAsync(CreateQualityInspectionRequest request);
}
