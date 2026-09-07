using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NcrsController : ControllerBase
{
    private readonly INcrService _ncrService;

    public NcrsController(INcrService ncrService)
    {
        _ncrService = ncrService;
    }

    /// <summary>Lists non-conformance reports.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<NcrResponse>>>> GetAll([FromQuery] string? status)
    {
        var result = await _ncrService.GetNcrsAsync(status);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific NCR by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<NcrResponse>>> GetById(int id)
    {
        var result = await _ncrService.GetNcrByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Manually creates a Non-Conformance Report.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<NcrResponse>>> Create([FromBody] CreateNcrRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _ncrService.CreateNcrAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Resolves an NCR with root cause and corrective action details.</summary>
    [HttpPut("{id:int}/resolve")]
    public async Task<ActionResult<ApiResponse<NcrResponse>>> Resolve(int id, [FromBody] ResolveNcrRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _ncrService.ResolveNcrAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
