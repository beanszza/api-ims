using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RtvsController : ControllerBase
{
    private readonly INcrService _ncrService;

    public RtvsController(INcrService ncrService)
    {
        _ncrService = ncrService;
    }

    /// <summary>Lists Return to Vendor (RTV) records.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<RtvResponse>>>> GetAll([FromQuery] string? status)
    {
        var result = await _ncrService.GetRtvsAsync(status);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets an RTV record by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<RtvResponse>>> GetById(int id)
    {
        var result = await _ncrService.GetRtvByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Creates a Return to Vendor authorization for rejected stock.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<RtvResponse>>> Create([FromBody] CreateRtvRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _ncrService.CreateRtvAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Dispatches rejected goods back to supplier and records stock reversal.</summary>
    [HttpPost("{id:int}/dispatch")]
    public async Task<ActionResult<ApiResponse<RtvResponse>>> Dispatch(int id, [FromBody] DispatchRtvRequest request)
    {
        var result = await _ncrService.DispatchRtvAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
