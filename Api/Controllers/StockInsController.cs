using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockInsController : ControllerBase
{
    private readonly IStockInService _stockInService;

    public StockInsController(IStockInService stockInService)
    {
        _stockInService = stockInService;
    }

    /// <summary>Lists Stock-Ins, optionally filtered by status or GRN ID.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<StockInResponse>>>> GetAll([FromQuery] string? status, [FromQuery] int? grnId)
    {
        var result = await _stockInService.GetStockInsAsync(status, grnId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific Stock-In record by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<StockInResponse>>> GetById(int id)
    {
        var result = await _stockInService.GetStockInByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Creates a Stock-In report (Draft or submitted for approval).</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<StockInResponse>>> Create([FromBody] CreateStockInRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _stockInService.CreateStockInAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Submits a Draft Stock-In for Admin approval.</summary>
    [HttpPost("{id:int}/submit")]
    public async Task<ActionResult<ApiResponse<StockInResponse>>> Submit(int id)
    {
        var result = await _stockInService.SubmitForApprovalAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Admin approves a Stock-In, committing items to Inventory and generating lots.</summary>
    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<ApiResponse<StockInResponse>>> Approve(int id, [FromBody] ApproveStockInRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _stockInService.ApproveStockInAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Admin rejects a Stock-In with reason.</summary>
    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<ApiResponse<StockInResponse>>> Reject(int id, [FromBody] RejectStockInRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _stockInService.RejectStockInAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
