using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class InventoryService : IInventoryService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(ScmDbContext context, ILogger<InventoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<PagedData<InventoryResponse>>> GetAllInventoriesAsync(string? categoryName = null, int page = 1, int pageSize = 10)
    {
        try
        {
            var query = _context.Inventories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                var catLower = categoryName.ToLower();
                if (catLower.Contains("raw material"))
                {
                    query = query.Where(i => (i.Item != null && i.Item.Category != null && i.Item.Category.CategoryName.ToLower().Contains("raw material")) && (i.Location == null || !i.Location.LocationName.ToLower().Contains("finished")));
                }
                else if (catLower.Contains("tool"))
                {
                    query = query.Where(i => i.Item != null && i.Item.Category != null && (i.Item.Category.CategoryName.ToLower().Contains("tool") || i.Item.Category.CategoryName.ToLower().Contains("equipment")));
                }
                else if (catLower.Contains("finished good"))
                {
                    query = query.Where(i => (i.Item != null && i.Item.Category != null && (i.Item.Category.CategoryName.ToLower().Contains("finished good") || i.Item.Category.CategoryName.ToLower().Contains("product"))) || (i.Location != null && i.Location.LocationName.ToLower().Contains("finished")));
                }
                else
                {
                    query = query.Where(i => i.Item != null && i.Item.Category != null && i.Item.Category.CategoryName.ToLower().Contains(catLower));
                }
            }

            var totalCount = await query.CountAsync();
            var inventories = await query
                .Include(i => i.Item)
                    .ThenInclude(it => it.Category)
                .Include(i => i.Item)
                    .ThenInclude(it => it.Uom)
                .Include(i => i.Location)
                .Select(i => new InventoryResponse
                {
                    InventoryId = i.InventoryId,
                    ItemId = i.ItemId,
                    ItemName = i.Item != null ? i.Item.ItemName : "Unknown Item",
                    CategoryName = i.Item != null && i.Item.Category != null ? i.Item.Category.CategoryName : "Unknown Category",
                    UomName = i.Item != null && i.Item.Uom != null ? i.Item.Uom.Abbreviation : "Unit",
                    LocationId = i.LocationId,
                    LocationName = i.Location != null ? i.Location.LocationName : "Unknown Location",
                    CurrentStock = i.CurrentStock,
                    MinStockLevel = i.Item != null ? i.Item.MinStockLevel : 0,
                    MaxStockLevel = i.Item != null ? i.Item.MaxStockLevel : 0
                })
                .OrderBy(i => i.InventoryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pagedData = new PagedData<InventoryResponse>
            {
                Items = inventories,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return ApiResponse<PagedData<InventoryResponse>>.SuccessResponse(pagedData, "Inventories retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching inventories.");
            return ApiResponse<PagedData<InventoryResponse>>.FailureResponse("An error occurred while retrieving inventory data.");
        }
    }
}
