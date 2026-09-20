using System;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly IItemService _itemService;

    public ItemsController(IItemService itemService)
    {
        _itemService = itemService;
    }
    // Task: Redirect to Order Module (Backend side)
    // Use route name "itemId" (not "id") so Swagger UI does not conflate this path param with GET /api/Items/{id}.
    [HttpGet("{itemId:int}/order-link")]
    public async Task<ActionResult<ApiResponse<ItemOrderLinkData>>> GetOrderRedirection([FromRoute] int itemId)
    {
        var item = await _itemService.GetItemByIdAsync(itemId);

        if (!item.Success)
        {
            return NotFound(item);
        }

        var redirectData = new ItemOrderLinkData
        {
            TargetModule = "OrderModule",
            Path = $"/orders/create?itemId={itemId}",
            ItemName = item.Data?.ItemName
        };

        return Ok(ApiResponse<ItemOrderLinkData>.SuccessResponse(redirectData, "Redirection data fetched"));
    }


    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedData<ItemResponse>>>> GetAllItems(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? sort = "asc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);
        var result = await _itemService.GetAllItemsAsync(search, category, isActive, sort, page, pageSize);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ItemResponse>>> GetItemById([FromRoute] int id)
    {
        var result = await _itemService.GetItemByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ApiResponse<ItemResponse>>> CreateItem([FromBody] CreateItemRequest request)
    {
        var result = await _itemService.CreateItemAsync(request);
        return result.Success
            ? CreatedAtAction(nameof(GetItemById), new { id = result.Data?.ItemId }, result)
            : StatusCode(400, result);
    }

    [HttpPut("{id}")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ApiResponse<ItemResponse>>> UpdateItem([FromRoute] int id, [FromBody] UpdateItemRequest request)
    {
        var result = await _itemService.UpdateItemAsync(id, request);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("{id}")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ApiResponse<EmptyPayload>>> DeleteItem([FromRoute] int id)
    {
        var result = await _itemService.DeleteItemAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}