using System.Collections.Generic;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupplierItemsController : ControllerBase
{
    private readonly ISupplierItemService _supplierItemService;

    public SupplierItemsController(ISupplierItemService supplierItemService)
    {
        _supplierItemService = supplierItemService;
    }

    /// <summary>
    /// Gets all catalog items provided by a specific supplier.
    /// GET /api/supplieritems/by-supplier/{supplierId}
    /// </summary>
    [HttpGet("by-supplier/{supplierId:int}")]
    public async Task<ActionResult<ApiResponse<List<SupplierItemResponse>>>> GetItemsBySupplier(int supplierId)
    {
        var result = await _supplierItemService.GetItemsBySupplierAsync(supplierId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Gets all active suppliers that provide a given item, sorted with preferred vendors first.
    /// Used by the PO creation UI when an item is selected to narrow the supplier dropdown.
    /// GET /api/supplieritems/by-item/{itemId}
    /// </summary>
    [HttpGet("by-item/{itemId:int}")]
    public async Task<ActionResult<ApiResponse<List<ItemSupplierOptionResponse>>>> GetSuppliersByItem(int itemId)
    {
        var result = await _supplierItemService.GetSuppliersByItemAsync(itemId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Gets a single supplier item entry.
    /// GET /api/supplieritems/{supplierId}/{itemId}
    /// </summary>
    [HttpGet("{supplierId:int}/{itemId:int}")]
    public async Task<ActionResult<ApiResponse<SupplierItemResponse>>> GetSupplierItem(int supplierId, int itemId)
    {
        var result = await _supplierItemService.GetSupplierItemAsync(supplierId, itemId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Adds or updates a supplier item catalog link.
    /// POST /api/supplieritems
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<SupplierItemResponse>>> UpsertSupplierItem([FromBody] CreateOrUpdateSupplierItemRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _supplierItemService.UpsertSupplierItemAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Removes a supplier item catalog link.
    /// DELETE /api/supplieritems/{supplierId}/{itemId}
    /// </summary>
    [HttpDelete("{supplierId:int}/{itemId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveSupplierItem(int supplierId, int itemId)
    {
        var result = await _supplierItemService.RemoveSupplierItemAsync(supplierId, itemId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
