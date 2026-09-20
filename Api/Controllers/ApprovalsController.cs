using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApprovalsController : ControllerBase
{
    private readonly IApprovalService _approvalService;

    public ApprovalsController(IApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    /// <summary>Gets all pending approvals requiring managerial sign-off.</summary>
    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<List<ApprovalRequestResponse>>>> GetPending()
    {
        var result = await _approvalService.GetPendingApprovalsAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets approval history with optional filtering by entity type and entity ID.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<List<ApprovalRequestResponse>>>> GetHistory([FromQuery] string? entityType, [FromQuery] int? entityId)
    {
        var result = await _approvalService.GetApprovalHistoryAsync(entityType, entityId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a single approval request by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ApprovalRequestResponse>>> GetById(int id)
    {
        var result = await _approvalService.GetApprovalByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Submits a new approval request.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ApprovalRequestResponse>>> Create([FromBody] CreateApprovalRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _approvalService.CreateApprovalRequestAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Records an approval or rejection decision.</summary>
    [HttpPost("{id:int}/act")]
    public async Task<ActionResult<ApiResponse<ApprovalRequestResponse>>> Act(int id, [FromBody] ActOnApprovalRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _approvalService.ActOnApprovalAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
