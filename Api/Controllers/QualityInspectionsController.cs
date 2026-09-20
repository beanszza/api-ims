using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QualityInspectionsController : ControllerBase
{
    private readonly IQualityInspectionService _qcService;

    public QualityInspectionsController(IQualityInspectionService qcService)
    {
        _qcService = qcService;
    }

    /// <summary>Lists quality inspections with optional type filtering.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<QualityInspectionResponse>>>> GetAll([FromQuery] string? type)
    {
        var result = await _qcService.GetInspectionsAsync(type);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific quality inspection by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<QualityInspectionResponse>>> GetById(int id)
    {
        var result = await _qcService.GetInspectionByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Records incoming QA inspection, releases approved stock to Available, and flags defects.</summary>
    [HttpPost("incoming")]
    public async Task<ActionResult<ApiResponse<QualityInspectionResponse>>> InspectIncoming([FromBody] CreateQualityInspectionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _qcService.InspectIncomingGoodsAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
