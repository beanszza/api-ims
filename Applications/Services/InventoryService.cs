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

    public async Task<ApiResponse<IEnumerable<InventoryResponse>>> GetAllInventoriesAsync()
    {
        try
        {
            var inventories = await _context.Inventories
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
                    MinStockLevel = i.Item != null ? i.Item.MinStockLevel : 0
                })
                .ToListAsync();

            return ApiResponse<IEnumerable<InventoryResponse>>.SuccessResponse(inventories, "Inventories retrieved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching inventories.");
            return ApiResponse<IEnumerable<InventoryResponse>>.FailureResponse("An error occurred while retrieving inventory data.");
        }
    }
}
