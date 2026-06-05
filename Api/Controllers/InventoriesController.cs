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
    public async Task<ActionResult<ApiResponse<IEnumerable<InventoryResponse>>>> GetAllInventories()
    {
        var result = await _inventoryService.GetAllInventoriesAsync();
        if (result.Success)
        {
            return Ok(result);
        }
        return StatusCode(500, result);
    }
}
