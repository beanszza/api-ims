using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupplierScorecardController : ControllerBase
{
    private readonly ISupplierScorecardService _scorecardService;

    public SupplierScorecardController(ISupplierScorecardService scorecardService)
    {
        _scorecardService = scorecardService;
    }

    /// <summary>Gets real-time scorecard metrics for all active suppliers.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<SupplierScorecardResponse>>>> GetAll()
    {
        var result = await _scorecardService.GetAllSupplierScorecardsAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets real-time scorecard metrics for a specific supplier.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<SupplierScorecardResponse>>> GetById(int id)
    {
        var result = await _scorecardService.GetSupplierScorecardAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
