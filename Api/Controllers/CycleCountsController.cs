using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

// [ApiController]
// [Route("api/[controller]")]
// Disabled as requested
public class CycleCountsController : ControllerBase
{
    private readonly ICycleCountService _cycleCountService;

    public CycleCountsController(ICycleCountService cycleCountService)
    {
        _cycleCountService = cycleCountService;
    }

    /// <summary>Lists all physical cycle counts.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CycleCountResponse>>>> GetCycleCounts([FromQuery] int? locationId = null)
    {
        var result = await _cycleCountService.GetCycleCountsAsync(locationId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific cycle count by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CycleCountResponse>>> GetById(int id)
    {
        var result = await _cycleCountService.GetCycleCountByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Records a new physical inventory count with observed quantities.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CycleCountResponse>>> Create([FromBody] CreateCycleCountRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _cycleCountService.CreateCycleCountAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Reconciles a cycle count, writing inventory adjustment ledgers and updating lot balances.</summary>
    [HttpPost("{id:int}/reconcile")]
    public async Task<ActionResult<ApiResponse<CycleCountResponse>>> Reconcile(int id, [FromBody] ReconcileCycleCountRequest? request = null)
    {
        var result = await _cycleCountService.ReconcileCycleCountAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Cancels a cycle count before reconciliation.</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<CycleCountResponse>>> Cancel(int id, [FromQuery] string reason)
    {
        var result = await _cycleCountService.CancelCycleCountAsync(id, reason);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
