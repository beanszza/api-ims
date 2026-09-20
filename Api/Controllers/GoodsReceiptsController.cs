using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GoodsReceiptsController : ControllerBase
{
    private readonly IGoodsReceiptService _grnService;

    public GoodsReceiptsController(IGoodsReceiptService grnService)
    {
        _grnService = grnService;
    }

    /// <summary>Lists Goods Receipt Notes, optionally filtered by PO.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<GoodsReceiptResponse>>>> GetAll([FromQuery] int? poId)
    {
        var result = await _grnService.GetGoodsReceiptsAsync(poId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific Goods Receipt Note by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<GoodsReceiptResponse>>> GetById(int id)
    {
        var result = await _grnService.GetGoodsReceiptByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Posts incoming goods against a PO, staging stock in Quarantine lots.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<GoodsReceiptResponse>>> Create([FromBody] CreateGoodsReceiptRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _grnService.ReceiveGoodsAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
