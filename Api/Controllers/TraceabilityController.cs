using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

// [ApiController]
// [Route("api/[controller]")]
// Disabled as requested
public class TraceabilityController : ControllerBase
{
    private readonly ITraceabilityService _traceabilityService;

    public TraceabilityController(ITraceabilityService traceabilityService)
    {
        _traceabilityService = traceabilityService;
    }

    /// <summary>Forward trace: Follows a raw material or packaging lot downstream to batches, FG lots, and branch shipments.</summary>
    [HttpGet("forward/{lotCode}")]
    public async Task<ActionResult<ApiResponse<ForwardTraceResponse>>> TraceForward(string lotCode)
    {
        var result = await _traceabilityService.TraceForwardAsync(lotCode);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Backward trace: Follows a finished goods lot upstream to manufacturing batch, ingredient/packaging lots, and suppliers.</summary>
    [HttpGet("backward/{fgLotCode}")]
    public async Task<ActionResult<ApiResponse<BackwardTraceResponse>>> TraceBackward(string fgLotCode)
    {
        var result = await _traceabilityService.TraceBackwardAsync(fgLotCode);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
