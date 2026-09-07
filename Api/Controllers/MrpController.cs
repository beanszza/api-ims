using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MrpController : ControllerBase
{
    private readonly IMrpService _mrpService;

    public MrpController(IMrpService mrpService)
    {
        _mrpService = mrpService;
    }

    /// <summary>Calculates net material requirements and automated purchase reorder suggestions based on planned production batches and safety stocks.</summary>
    [HttpPost("plan")]
    public async Task<ActionResult<ApiResponse<MrpPlanResponse>>> GeneratePlan([FromBody] GenerateMrpPlanRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _mrpService.GeneratePlanAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
