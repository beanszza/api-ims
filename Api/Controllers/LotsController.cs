using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LotsController : ControllerBase
{
    private readonly ILotService _lotService;

    public LotsController(ILotService lotService)
    {
        _lotService = lotService;
    }

    /// <summary>Paginated flat list of all lots, FEFO ordered.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedData<LotResponse>>>> GetAllLots(
        [FromQuery] string? itemName = null,
        [FromQuery] string? locationName = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _lotService.GetAllLotsAsync(itemName, locationName, status, page, pageSize);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// All lots for a single item, FEFO sorted with SharePercent and IsFefoNext.
    /// Used by the inventory grid's expandable row to show per-lot detail.
    /// GET /api/lots/byitem/{itemId}
    /// </summary>
    [HttpGet("byitem/{itemId:int}")]
    public async Task<ActionResult<ApiResponse<List<LotResponse>>>> GetLotsByItem(int itemId)
    {
        var result = await _lotService.GetLotsByItemAsync(itemId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}