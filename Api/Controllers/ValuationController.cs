using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

// [ApiController]
// [Route("api/[controller]")]
// Disabled as requested
public class ValuationController : ControllerBase
{
    private readonly IValuationService _valuationService;

    public ValuationController(IValuationService valuationService)
    {
        _valuationService = valuationService;
    }

    /// <summary>Gets a real-time inventory valuation report across locations, item categories, and lots.</summary>
    [HttpGet("report")]
    public async Task<ActionResult<ApiResponse<InventoryValuationReportResponse>>> GetReport(
        [FromQuery] int? locationId = null, [FromQuery] int? categoryId = null)
    {
        var result = await _valuationService.GetValuationReportAsync(locationId, categoryId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
