using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecallController : ControllerBase
{
    private readonly IRecallService _recallService;

    public RecallController(IRecallService recallService)
    {
        _recallService = recallService;
    }

    /// <summary>Executes a mock recall simulation or active quarantine hold cascade on a defective lot.</summary>
    [HttpPost("simulate")]
    public async Task<ActionResult<ApiResponse<RecallSimulationResponse>>> SimulateRecall([FromBody] SimulateRecallRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _recallService.SimulateRecallAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Lists historical mock recall simulations and hold records.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<List<RecallRecord>>>> GetHistory()
    {
        var result = await _recallService.GetRecallHistoryAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
