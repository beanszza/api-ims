using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _purchaseOrderService;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrderService)
    {
        _purchaseOrderService = purchaseOrderService;
    }

    [HttpPost]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequest request)
    {
        var result = await _purchaseOrderService.CreatePurchaseOrderAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedData<PurchaseOrderResponse>>>> GetPurchaseOrders(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? eligibleForDelivery = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);
        var result = await _purchaseOrderService.GetPurchaseOrdersAsync(status, search, page, pageSize, eligibleForDelivery);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> GetPurchaseOrderById([FromRoute] int id)
    {
        var result = await _purchaseOrderService.GetPurchaseOrderByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPut("{id}/status")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> UpdateOrderStatus([FromRoute] int id, [FromBody] UpdatePurchaseOrderQaRequest request)
    {
        var result = await _purchaseOrderService.UpdateOrderStatusAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> UpdatePurchaseOrder([FromRoute] int id, [FromBody] CreatePurchaseOrderRequest request)
    {
        var result = await _purchaseOrderService.UpdatePurchaseOrderAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/upload-receipt")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> UploadReceipt([FromRoute] int id, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<PurchaseOrderResponse>.FailureResponse("No file was uploaded."));
        
        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(ApiResponse<PurchaseOrderResponse>.FailureResponse("Receipt file size cannot exceed 10MB."));
            
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
        if (!allowedExtensions.Contains(ext))
            return BadRequest(ApiResponse<PurchaseOrderResponse>.FailureResponse("Invalid file type. Allowed formats: JPG, PNG, WEBP, PDF."));

        var result = await _purchaseOrderService.UploadReceiptAsync(id, file);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}/receipt")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> DeleteReceipt([FromRoute] int id)
    {
        var result = await _purchaseOrderService.DeleteReceiptAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<ApiResponse<PagedData<TransactionHistoryResponse>>>> GetTransactionHistory(
        [FromQuery] string? filterType = null,
        [FromQuery] DateTime? specificDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);
        var result = await _purchaseOrderService.GetTransactionHistoryAsync(filterType, specificDate, page, pageSize);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    [HttpGet("transactions/export")]
    [EnableRateLimiting("export")]
    public async Task<IActionResult> ExportTransactionHistory(
        [FromQuery] string? filterType = null,
        [FromQuery] DateTime? specificDate = null)
    {
        try
        {
            var csvContent = await _purchaseOrderService.ExportTransactionHistoryCsvAsync(filterType, specificDate);
            var fileBytes = Encoding.UTF8.GetBytes(csvContent);
            return File(fileBytes, "text/csv", "transaction_history.csv");
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<EmptyPayload>.FailureResponse($"Failed to export CSV: {ex.Message}"));
        }
    }

    /// <summary>
    /// Returns the total quantity already ordered per item across all non-cancelled POs for a given PR.
    /// Used by the frontend to enforce per-item ordering limits when creating new POs.
    /// </summary>
    [HttpGet("pr/{prId:int}/ordered-qty")]
    public async Task<ActionResult<ApiResponse<List<PrItemOrderedQtyResponse>>>> GetOrderedQtyForPr([FromRoute] int prId)
    {
        var result = await _purchaseOrderService.GetOrderedQtyForPrAsync(prId);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    /// <summary>
    /// Returns the next sequential PO number that will be assigned to a new Purchase Order.
    /// </summary>
    [HttpGet("next-number")]
    public async Task<ActionResult<ApiResponse<string>>> GetNextPoNumber()
    {
        var result = await _purchaseOrderService.GetNextPoNumberPreviewAsync();
        return result.Success ? Ok(result) : StatusCode(500, result);
    }
}
