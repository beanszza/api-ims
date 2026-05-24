using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface ISupplierService
{
    Task<ApiResponse<IEnumerable<SupplierResponse>>> GetAllSuppliersAsync(string? supplierName = null, bool? isActive = null);
    Task<ApiResponse<SupplierResponse>> GetSupplierByIdAsync(int id);
    Task<ApiResponse<SupplierResponse>> CreateSupplierAsync(CreateSupplierRequest request);
    Task<ApiResponse<SupplierResponse>> UpdateSupplierAsync(int id, UpdateSupplierRequest request);
    Task<ApiResponse<EmptyPayload>> DeleteSupplierAsync(int id);
}