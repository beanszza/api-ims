using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchaseRequisitionsController : ControllerBase
{
    private readonly IPurchaseRequisitionService _prService;

    public PurchaseRequisitionsController(IPurchaseRequisitionService prService)
    {
        _prService = prService;
    }

    /// <summary>Lists purchase requisitions with optional status filtering.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PurchaseRequisitionResponse>>>> GetAll([FromQuery] string? status)
    {
        var result = await _prService.GetPurchaseRequisitionsAsync(status);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific purchase requisition with line items.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PurchaseRequisitionResponse>>> GetById(int id)
    {
        var result = await _prService.GetPurchaseRequisitionByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Creates a new purchase requisition.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PurchaseRequisitionResponse>>> Create([FromBody] CreatePurchaseRequisitionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _prService.CreatePurchaseRequisitionAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Updates the status of a purchase requisition (e.g., Pending Approval, Approved, Rejected).</summary>
    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<PurchaseRequisitionResponse>>> UpdateStatus(int id, [FromBody] UpdatePurchaseRequisitionStatusRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _prService.UpdateStatusAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Converts an approved purchase requisition into multiple supplier Purchase Orders
    /// using catalog preferred vendor mappings.
    /// </summary>
    [HttpPost("{id:int}/fan-out")]
    public async Task<ActionResult<ApiResponse<PrFanOutResultResponse>>> FanOutToPurchaseOrders(int id)
    {
        var result = await _prService.FanOutToPurchaseOrdersAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
