using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiscrepanciesController : ControllerBase
{
    private readonly IDiscrepancyService _discrepancyService;

    public DiscrepanciesController(IDiscrepancyService discrepancyService)
    {
        _discrepancyService = discrepancyService;
    }

    /// <summary>Lists discrepancies, optionally filtered by type, status, GRN, or PO.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<DiscrepancyResponse>>>> GetAll(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int? grnId,
        [FromQuery] int? poId)
    {
        var result = await _discrepancyService.GetDiscrepanciesAsync(type, status, grnId, poId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific discrepancy by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<DiscrepancyResponse>>> GetById(int id)
    {
        var result = await _discrepancyService.GetDiscrepancyByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Resolves a discrepancy with the chosen resolution type and notes.</summary>
    [HttpPost("{id:int}/resolve")]
    public async Task<ActionResult<ApiResponse<DiscrepancyResponse>>> Resolve(
        int id,
        [FromBody] ResolveDiscrepancyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _discrepancyService.ResolveAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Creates a Loss Report to write off quantity from a discrepancy.</summary>
    [HttpPost("{id:int}/loss-report")]
    public async Task<ActionResult<ApiResponse<LossReportResponse>>> CreateLossReport(
        int id,
        [FromBody] CreateLossReportRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _discrepancyService.CreateLossReportAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Creates a Return to Supplier (RTV) shipment task from a discrepancy.</summary>
    [HttpPost("{id:int}/return-to-supplier")]
    public async Task<ActionResult<ApiResponse<DiscrepancyResponse>>> ReturnToSupplier(
        int id,
        [FromBody] CreateRtvFromDiscrepancyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _discrepancyService.CreateRtvAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
