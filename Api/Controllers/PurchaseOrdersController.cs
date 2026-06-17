using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequest request)
    {
        var result = await _purchaseOrderService.CreatePurchaseOrderAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedData<PurchaseOrderResponse>>>> GetPurchaseOrders([FromQuery] string? status = null, [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _purchaseOrderService.GetPurchaseOrdersAsync(status, search, page, pageSize);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> UpdateOrderStatus([FromRoute] int id, [FromBody] UpdatePurchaseOrderQaRequest request)
    {
        var result = await _purchaseOrderService.UpdateOrderStatusAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> UpdatePurchaseOrder([FromRoute] int id, [FromBody] CreatePurchaseOrderRequest request)
    {
        var result = await _purchaseOrderService.UpdatePurchaseOrderAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/upload-receipt")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderResponse>>> UploadReceipt([FromRoute] int id, [FromForm] IFormFile file)
    {
        var result = await _purchaseOrderService.UploadReceiptAsync(id, file);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}/receipt")]
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
        var result = await _purchaseOrderService.GetTransactionHistoryAsync(filterType, specificDate, page, pageSize);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    [HttpGet("transactions/export")]
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
}
