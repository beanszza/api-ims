using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface ISupplierDocumentService
{
    Task<ApiResponse<List<SupplierDocumentResponse>>> GetDocumentsBySupplierAsync(int supplierId);
    Task<ApiResponse<SupplierDocumentResponse>> GetDocumentByIdAsync(int documentId);
    Task<ApiResponse<SupplierComplianceSummaryResponse>> GetComplianceSummaryAsync(int supplierId);
    Task<ApiResponse<SupplierDocumentResponse>> CreateDocumentAsync(CreateSupplierDocumentRequest request);
    Task<ApiResponse<SupplierDocumentResponse>> VerifyDocumentAsync(VerifySupplierDocumentRequest request);
    Task<ApiResponse<bool>> DeleteDocumentAsync(int documentId);
}
