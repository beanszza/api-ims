using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FinishedProductsController : ControllerBase
{
    private readonly IFinishedProductService _finishedProductService;

    public FinishedProductsController(IFinishedProductService finishedProductService)
    {
        _finishedProductService = finishedProductService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<FinishedProductResponse>>>> GetAllFinishedProducts()
    {
        var result = await _finishedProductService.GetAllFinishedProductsAsync();
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FinishedProductResponse>>> CreateFinishedProduct([FromBody] api_scm.Contracts.Requests.CreateFinishedProductRequest request)
    {
        var result = await _finishedProductService.CreateFinishedProductAsync(request);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<FinishedProductResponse>>> UpdateFinishedProduct(int id, [FromBody] api_scm.Contracts.Requests.UpdateFinishedProductRequest request)
    {
        var result = await _finishedProductService.UpdateFinishedProductAsync(id, request);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteFinishedProduct(int id)
    {
        var result = await _finishedProductService.DeleteFinishedProductAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/image")]
    public async Task<IActionResult> UploadImage(int id, IFormFile file, [FromServices] Infrastructures.Persistence.ScmDbContext dbContext)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = "Image file size cannot exceed 10MB." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Invalid file type. Only JPG, JPEG, PNG, and WEBP are allowed." });

            var product = await dbContext.FinishedProducts.FindAsync(id);
            if (product == null)
                return NotFound(new { message = "Product not found." });

            var uploadsFolder = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");
            if (!System.IO.Directory.Exists(uploadsFolder)) System.IO.Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = System.IO.Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            product.ImageUrl = $"/uploads/products/{uniqueFileName}";
            await dbContext.SaveChangesAsync();

            return Ok(new { imageUrl = product.ImageUrl });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
