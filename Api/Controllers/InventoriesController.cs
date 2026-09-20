using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoriesController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoriesController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedData<InventoryResponse>>>> GetAllInventories([FromQuery] string? categoryName = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        pageSize = System.Math.Clamp(pageSize, 1, 100);
        page = System.Math.Max(page, 1);
        var result = await _inventoryService.GetAllInventoriesAsync(categoryName, page, pageSize);
        if (result.Success)
        {
            return Ok(result);
        }
        return StatusCode(500, result);
    }
}
