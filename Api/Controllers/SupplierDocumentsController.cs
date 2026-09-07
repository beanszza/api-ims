using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupplierDocumentsController : ControllerBase
{
    private readonly ISupplierDocumentService _documentService;

    public SupplierDocumentsController(ISupplierDocumentService documentService)
    {
        _documentService = documentService;
    }

    /// <summary>Gets all regulatory documents for a supplier.</summary>
    [HttpGet("by-supplier/{supplierId:int}")]
    public async Task<ActionResult<ApiResponse<List<SupplierDocumentResponse>>>> GetDocumentsBySupplier(int supplierId)
    {
        var result = await _documentService.GetDocumentsBySupplierAsync(supplierId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets compliance status summary (FDA LTO, Sanitary Permit, expiry counts) for a supplier.</summary>
    [HttpGet("compliance-summary/{supplierId:int}")]
    public async Task<ActionResult<ApiResponse<SupplierComplianceSummaryResponse>>> GetComplianceSummary(int supplierId)
    {
        var result = await _documentService.GetComplianceSummaryAsync(supplierId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Gets a specific document by ID.</summary>
    [HttpGet("{documentId:int}")]
    public async Task<ActionResult<ApiResponse<SupplierDocumentResponse>>> GetDocumentById(int documentId)
    {
        var result = await _documentService.GetDocumentByIdAsync(documentId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Registers a new supplier document.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<SupplierDocumentResponse>>> CreateDocument([FromBody] CreateSupplierDocumentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _documentService.CreateDocumentAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Verifies or rejects a supplier document.</summary>
    [HttpPost("verify")]
    public async Task<ActionResult<ApiResponse<SupplierDocumentResponse>>> VerifyDocument([FromBody] VerifySupplierDocumentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _documentService.VerifyDocumentAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Deletes a supplier document.</summary>
    [HttpDelete("{documentId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteDocument(int documentId)
    {
        var result = await _documentService.DeleteDocumentAsync(documentId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
