using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface ISupplierService
{
    Task<ApiResponse<PagedData<SupplierResponse>>> GetAllSuppliersAsync(string? supplierName = null, bool? isActive = null, int page = 1, int pageSize = 10);
    Task<ApiResponse<SupplierResponse>> GetSupplierByIdAsync(int id);
    Task<ApiResponse<SupplierResponse>> CreateSupplierAsync(CreateSupplierRequest request);
    Task<ApiResponse<SupplierResponse>> UpdateSupplierAsync(int id, UpdateSupplierRequest request);
    Task<ApiResponse<EmptyPayload>> DeleteSupplierAsync(int id);
}