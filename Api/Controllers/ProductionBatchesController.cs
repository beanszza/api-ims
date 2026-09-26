using Api.Contracts.Production;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductionBatchesController : ControllerBase
{
    private readonly IProductionService _productionService;

    public ProductionBatchesController(IProductionService productionService)
    {
        _productionService = productionService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateBatch([FromBody] CreateProductionBatchRequest request)
    {
        try
        {
            var result = await _productionService.CreateBatchAsync(request);
            return CreatedAtAction(nameof(GetBatches), new { id = result.BatchId }, result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetBatches()
    {
        try
        {
            var result = await _productionService.GetAllBatchesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBatch(int id)
    {
        try
        {
            var result = await _productionService.GetBatchByIdAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/approve")]
    public async Task<IActionResult> ApproveBatch(int id)
    {
        try
        {
            var result = await _productionService.ApproveBatchAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/reject")]
    public async Task<IActionResult> RejectBatch(int id, [FromBody] RejectBatchRequest request)
    {
        try
        {
            var result = await _productionService.RejectBatchAsync(id, request.Reason);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelBatch(int id)
    {
        try
        {
            var result = await _productionService.CancelBatchAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/stage")]
    public async Task<IActionResult> UpdateStage(int id, [FromBody] UpdateStageRequest request)
    {
        try
        {
            var result = await _productionService.UpdateStageAsync(id, request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/images")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> UploadImage(int id, IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = "Image file size cannot exceed 10MB." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Invalid file type. Only JPG, JPEG, PNG, and WEBP are allowed." });

            // Pass the raw IFormFile down to the service for database storage
            var result = await _productionService.UploadImageAsync(id, file);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardSummary()
    {
        try
        {
            var result = await _productionService.GetDashboardSummaryAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/qa-notes")]
    public async Task<IActionResult> SubmitQaNotes(int id, [FromBody] UpdateQaNotesRequest request)
    {
        try
        {
            var result = await _productionService.SubmitQaAsync(id, request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/qa-status")]
    public async Task<IActionResult> UpdateQaApproval(int id, [FromBody] UpdateQaApprovalRequest request)
    {
        try
        {
            var result = await _productionService.UpdateQaApprovalAsync(id, request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/add-to-inventory")]
    public async Task<IActionResult> AddToInventory(int id)
    {
        try
        {
            var result = await _productionService.AddBatchToInventoryAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStockAlerts()
    {
        try
        {
            var result = await _productionService.GetLowStockAlertsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
