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
}
