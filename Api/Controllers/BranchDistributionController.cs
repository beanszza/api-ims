using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BranchDistributionController : ControllerBase
{
    private readonly IBranchDistributionService _distributionService;

    public BranchDistributionController(IBranchDistributionService distributionService)
    {
        _distributionService = distributionService;
    }

    /// <summary>Lists all branch replenishment requests.</summary>
    [HttpGet("requests")]
    public async Task<ActionResult<ApiResponse<List<BranchRequestResponse>>>> GetRequests(
        [FromQuery] int? branchId = null, [FromQuery] string? status = null)
    {
        var result = await _distributionService.GetBranchRequestsAsync(branchId, status);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific branch replenishment request by ID.</summary>
    [HttpGet("requests/{id:int}")]
    public async Task<ActionResult<ApiResponse<BranchRequestResponse>>> GetRequestById(int id)
    {
        var result = await _distributionService.GetBranchRequestByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Submits a new store replenishment request from a branch.</summary>
    [HttpPost("requests")]
    public async Task<ActionResult<ApiResponse<BranchRequestResponse>>> CreateRequest([FromBody] CreateBranchRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _distributionService.CreateBranchRequestAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Approves a branch request and generates a dispatch shipment order.</summary>
    [HttpPost("requests/{id:int}/approve")]
    public async Task<ActionResult<ApiResponse<StockTransferResponse>>> ApproveRequest(
        int id, [FromBody] DispatchShipmentRequest? dispatchDetails = null)
    {
        var result = await _distributionService.ApproveAndGenerateShipmentAsync(id, dispatchDetails);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Rejects a branch replenishment request with a reason.</summary>
    [HttpPost("requests/{id:int}/reject")]
    public async Task<ActionResult<ApiResponse<BranchRequestResponse>>> RejectRequest(
        int id, [FromQuery] string reason)
    {
        var result = await _distributionService.RejectBranchRequestAsync(id, reason);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Lists all branch returns.</summary>
    [HttpGet("returns")]
    public async Task<ActionResult<ApiResponse<List<BranchReturnResponse>>>> GetReturns([FromQuery] int? branchId = null)
    {
        var result = await _distributionService.GetBranchReturnsAsync(branchId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific branch return by ID.</summary>
    [HttpGet("returns/{id:int}")]
    public async Task<ActionResult<ApiResponse<BranchReturnResponse>>> GetReturnById(int id)
    {
        var result = await _distributionService.GetBranchReturnByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Records a return of damaged / unsold goods from a branch.</summary>
    [HttpPost("returns")]
    public async Task<ActionResult<ApiResponse<BranchReturnResponse>>> CreateReturn([FromBody] CreateBranchReturnRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _distributionService.CreateBranchReturnAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Receives and confirms a branch return at commissary warehouse.</summary>
    [HttpPost("returns/{id:int}/receive")]
    public async Task<ActionResult<ApiResponse<BranchReturnResponse>>> ReceiveReturn(int id)
    {
        var result = await _distributionService.ReceiveBranchReturnAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
