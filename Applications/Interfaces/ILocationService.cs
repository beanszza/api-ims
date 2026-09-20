using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface ILocationService
{
    Task<ApiResponse<PagedData<LocationResponse>>> GetAllLocationsAsync(int page = 1, int pageSize = 10);

    // The acting user is resolved from the request by ICurrentUserService rather than passed in.
    // Controllers used to supply a literal 1, which is how the audit trail came to credit every change
    // in the system to the same person.
    Task<ApiResponse<LocationResponse>> CreateLocationAsync(CreateLocationRequest request);
    Task<ApiResponse<LocationResponse>> UpdateLocationAsync(int id, UpdateLocationRequest request);
}
