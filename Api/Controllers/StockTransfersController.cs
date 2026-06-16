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
    public async Task<ActionResult<ApiResponse<IEnumerable<StockTransferResponse>>>> GetAllTransfers()
    {
        var result = await _stockTransferService.GetAllTransfersAsync();
        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<StockTransferResponse>>> CreateTransfer([FromBody] CreateStockTransferRequest request)
    {
        // For testing/mocking, assuming UserId = 1
        var result = await _stockTransferService.CreateTransferAsync(request, userId: 1);
        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<ApiResponse<StockTransferResponse>>> UpdateTransferStatus([FromRoute] int id, [FromBody] UpdateStockTransferStatusRequest request)
    {
        // For testing/mocking, assuming UserId = 1
        var result = await _stockTransferService.UpdateTransferStatusAsync(id, request, userId: 1);
        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<TransferDashboardResponse>>> GetDashboardSummary()
    {
        var result = await _stockTransferService.GetTransferDashboardSummaryAsync();
        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TransferHistoryResponse>>>> GetTransferHistory([FromQuery] string? status, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var result = await _stockTransferService.GetTransferHistoryAsync(status, fromDate, toDate);
        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    [HttpGet("export-history")]
    public async Task<IActionResult> ExportTransferHistoryCsv([FromQuery] string? status, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var result = await _stockTransferService.GetTransferHistoryAsync(status, fromDate, toDate);
        
        if (!result.Success || result.Data == null)
            return BadRequest(result);

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("LogId,TransferId,Action,FieldName,OldValue,NewValue,Timestamp,UserId");

        foreach (var log in result.Data)
        {
            builder.AppendLine($"{log.LogId},{log.TransferId},{log.Action},{log.FieldName},{log.OldValue},{log.NewValue},{log.Timestamp:yyyy-MM-dd HH:mm:ss},{log.UserId}");
        }

        return File(System.Text.Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "transfer_history.csv");
    }
}
