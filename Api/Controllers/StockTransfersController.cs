using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _stockTransferService;

    public StockTransfersController(IStockTransferService stockTransferService)
    {
        _stockTransferService = stockTransferService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedData<StockTransferResponse>>>> GetAllTransfers([FromQuery] string? status = null, [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _stockTransferService.GetAllTransfersAsync(status, search, page, pageSize);
        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<StockTransferResponse>>> CreateTransfer([FromBody] CreateStockTransferRequest request)
    {
        var result = await _stockTransferService.CreateTransferAsync(request);
        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<StockTransferResponse>>> UpdateTransfer([FromRoute] int id, [FromBody] UpdateStockTransferRequest request)
    {
        var result = await _stockTransferService.UpdateTransferAsync(id, request);
        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<ApiResponse<StockTransferResponse>>> UpdateTransferStatus([FromRoute] int id, [FromBody] UpdateStockTransferStatusRequest request)
    {
        var result = await _stockTransferService.UpdateTransferStatusAsync(id, request);
        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<TransferDashboardResponse>>> GetDashboardSummary()
    {
        var result = await _stockTransferService.GetTransferDashboardSummaryAsync();
        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<PagedData<TransferHistoryResponse>>>> GetTransferHistory([FromQuery] string? status, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _stockTransferService.GetTransferHistoryAsync(status, fromDate, toDate, page, pageSize);
        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    [HttpGet("export-history")]
    public async Task<IActionResult> ExportTransferHistoryCsv([FromQuery] string? status, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        // For CSV export, we fetch all data by passing max values (or we can use the non-paged if service allowed it).
        // Passing page=1, pageSize=int.MaxValue to get all logs
        var result = await _stockTransferService.GetTransferHistoryAsync(status, fromDate, toDate, 1, int.MaxValue);
        
        if (!result.Success || result.Data == null || result.Data.Items == null)
            return BadRequest(result);

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("LogId,TransferId,Action,FieldName,OldValue,NewValue,Timestamp,UserId");

        foreach (var log in result.Data.Items)
        {
            builder.AppendLine($"{log.LogId},{log.TransferId},{log.Action},{log.FieldName},{log.OldValue},{log.NewValue},{log.Timestamp:yyyy-MM-dd HH:mm:ss},{log.UserId}");
        }

        return File(System.Text.Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "transfer_history.csv");
    }
}
