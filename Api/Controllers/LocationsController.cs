using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _locationService;

    public LocationsController(ILocationService locationService)
    {
        _locationService = locationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedData<LocationResponse>>>> GetAllLocations([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _locationService.GetAllLocationsAsync(page, pageSize);
        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<LocationResponse>>> UpdateLocation([FromRoute] int id, [FromBody] UpdateLocationRequest request)
    {
        // For testing/mocking, assuming UserId = 1
        var result = await _locationService.UpdateLocationAsync(id, request, userId: 1);
        return result.Success ? Ok(result) : StatusCode(400, result);
    }
}
