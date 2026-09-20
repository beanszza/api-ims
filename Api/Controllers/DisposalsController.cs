using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DisposalsController : ControllerBase
{
    private readonly IDisposalService _disposalService;

    public DisposalsController(IDisposalService disposalService)
    {
        _disposalService = disposalService;
    }

    /// <summary>Lists all disposal and stock write-off records.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<DisposalRecordResponse>>>> GetAll()
    {
        var result = await _disposalService.GetDisposalRecordsAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific disposal record by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<DisposalRecordResponse>>> GetById(int id)
    {
        var result = await _disposalService.GetDisposalRecordByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Manually creates a stock disposal write-off.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<DisposalRecordResponse>>> Create([FromBody] CreateDisposalRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _disposalService.CreateDisposalRecordAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Executes an automated expiry sweep, quarantining expired lots and debiting stock.</summary>
    [HttpPost("expiry-sweep")]
    public async Task<ActionResult<ApiResponse<ExpirySweepResponse>>> RunExpirySweep()
    {
        var result = await _disposalService.PerformExpirySweepAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
