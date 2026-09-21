using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PutAwayController : ControllerBase
{
    private readonly IPutAwayService _putAwayService;

    public PutAwayController(IPutAwayService putAwayService)
    {
        _putAwayService = putAwayService;
    }

    /// <summary>Lists Put Away tasks with optional filtering by status or GRN.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PutAwayResponse>>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] int? grnId)
    {
        var result = await _putAwayService.GetPutAwaysAsync(status, grnId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific Put Away task by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PutAwayResponse>>> GetById(int id)
    {
        var result = await _putAwayService.GetPutAwayByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Completes a Put Away task, assigns location/lot, and releases inventory to Available.</summary>
    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<ApiResponse<PutAwayResponse>>> Complete(
        int id,
        [FromBody] CompletePutAwayRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _putAwayService.CompletePutAwayAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Batch completes all Put Away tasks for a GRN.</summary>
    [HttpPost("batch-complete")]
    public async Task<ActionResult<ApiResponse<List<PutAwayResponse>>>> BatchComplete(
        [FromBody] BatchCompletePutAwayRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _putAwayService.BatchCompletePutAwayAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
