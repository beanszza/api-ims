using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface ILocationService
{
    Task<ApiResponse<IEnumerable<LocationResponse>>> GetAllLocationsAsync();
    Task<ApiResponse<LocationResponse>> UpdateLocationAsync(int id, UpdateLocationRequest request, int userId);
}
