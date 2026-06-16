using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Domains.Entities;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Applications.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Applications.Services;

public class ItemService : IItemService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<ItemService> _logger;

    public ItemService(ScmDbContext context, ILogger<ItemService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<PagedData<ItemResponse>>> GetAllItemsAsync(string? search = null, string? category = null, bool? isActive = null, string? sort = "asc", int page = 1, int pageSize = 10)
    {
        try
        {
            _logger.LogInformation("Fetching and filtering items");

            if (search != null && search.Length > 200)
            {
                return ApiResponse<PagedData<ItemResponse>>.FailureResponse("Search query cannot exceed 200 characters.");
            }

            var query = _context.Items.AsQueryable();

            //Filter by Category and Status
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(i => i.Category!.CategoryName == category);
            }

            if (isActive.HasValue)
            {
                query = query.Where(i => i.IsActive == isActive.Value);
            }

            var items = await query
                .Select(i => new ItemResponse
                {
                    ItemId = i.ItemId,
                    ItemName = i.ItemName,
                    UomId = i.UomId,
                    CategoryId = i.CategoryId,
                    UomName = i.Uom!.Name,
                    CategoryName = i.Category!.CategoryName,
                    CurrentStock = i.Inventories.Sum(inv => inv.CurrentStock),
                    MinStockLevel = i.MinStockLevel,
                    MaxStockLevel = i.MaxStockLevel,
                    IsActive = i.IsActive
                })
                .ToListAsync();

            //Search by Name, Category, Quantity, or all combined (case-insensitive)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                items = items.Where(item =>
                {
                    return terms.All(term =>
                    {
                        var lowerTerm = term.ToLowerInvariant();
                        bool nameMatch = item.ItemName.ToLowerInvariant().Contains(lowerTerm);
                        bool categoryMatch = item.CategoryName.ToLowerInvariant().Contains(lowerTerm);
                        bool quantityMatch = item.CurrentStock.ToString().Contains(lowerTerm);
                        return nameMatch || categoryMatch || quantityMatch;
                    });
                }).ToList();
            }

            //Sort alphabetically (default Ascending)
            if (string.Equals(sort, "desc", StringComparison.OrdinalIgnoreCase))
            {
                items = items.OrderByDescending(i => i.ItemName).ToList();
            }
            else
            {
                items = items.OrderBy(i => i.ItemName).ToList();
            }

            var totalCount = items.Count;
            var pagedItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var pagedData = new PagedData<ItemResponse>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return ApiResponse<PagedData<ItemResponse>>.SuccessResponse(pagedData);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching items: {ex.Message}");
            return ApiResponse<PagedData<ItemResponse>>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ItemResponse>> GetItemByIdAsync(int id)
    {
        try
        {
            _logger.LogInformation($"Fetching item with ID: {id}");

            var item = await _context.Items
                .Include(i => i.Uom)
                .Include(i => i.Category)
                .FirstOrDefaultAsync(i => i.ItemId == id);

            if (item == null)
            {
                _logger.LogWarning($"Item with ID {id} not found");
                return ApiResponse<ItemResponse>.FailureResponse("Item not found");
            }

            var currentStock = await _context.Inventories.AsNoTracking()
                .Where(inv => inv.ItemId == id)
                .SumAsync(inv => (int?)inv.CurrentStock) ?? 0;

            var response = new ItemResponse
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                UomId = item.UomId,
                CategoryId = item.CategoryId,
                MinStockLevel = item.MinStockLevel,
                MaxStockLevel = item.MaxStockLevel,
                IsActive = item.IsActive,
                UomName = item.Uom!.Name,
                CategoryName = item.Category!.CategoryName,
                CurrentStock = currentStock
            };

            return ApiResponse<ItemResponse>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching item: {ex.Message}");
            return ApiResponse<ItemResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ItemResponse>> CreateItemAsync(CreateItemRequest request)
    {
        try
        {
            _logger.LogInformation($"Creating new item: {request.ItemName}");

            // Validate duplicate name
            var normalizedName = request.ItemName.Trim().ToLower();
            var nameExists = await _context.Items.AnyAsync(i => i.ItemName.Trim().ToLower() == normalizedName);
            if (nameExists)
            {
                return ApiResponse<ItemResponse>.FailureResponse("An item with this name already exists.");
            }

            // Validate UOM and Category exist
            var uomExists = await _context.UnitOfMeasures.AnyAsync(u => u.UomId == request.UomId);

           
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId);

            if (!uomExists || category == null)
            {
                _logger.LogWarning("Invalid UOM or Category ID");
                return ApiResponse<ItemResponse>.FailureResponse("Invalid UOM or Category ID");
            }

            if (category.CategoryName != "Raw Materials" && category.CategoryName != "Tools and Supplies")
            {
                return ApiResponse<ItemResponse>.FailureResponse("Category must be 'Raw Materials' or 'Tools and Supplies'.");
            }

            var item = new Item
            {
                ItemName = request.ItemName,
                UomId = request.UomId,
                CategoryId = request.CategoryId,
                MinStockLevel = request.MinStockLevel,
                MaxStockLevel = request.MaxStockLevel,
                IsActive = request.IsActive
            };

            _context.Items.Add(item);
            await _context.SaveChangesAsync();

            await _context.Entry(item).Reference(i => i.Uom).LoadAsync();
            await _context.Entry(item).Reference(i => i.Category).LoadAsync();

            var currentStock = await _context.Inventories.AsNoTracking()
                .Where(inv => inv.ItemId == item.ItemId)
                .SumAsync(inv => (int?)inv.CurrentStock) ?? 0;

            var response = new ItemResponse
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                UomId = item.UomId,
                CategoryId = item.CategoryId,
                MinStockLevel = item.MinStockLevel,
                MaxStockLevel = item.MaxStockLevel,
                IsActive = item.IsActive,
                UomName = item.Uom!.Name,
                CategoryName = item.Category!.CategoryName,
                CurrentStock = currentStock
            };

            _logger.LogInformation($"Item created successfully with ID: {item.ItemId}");
            return ApiResponse<ItemResponse>.SuccessResponse(response, "Item created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating item: {ex.Message}");
            return ApiResponse<ItemResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ItemResponse>> UpdateItemAsync(int id, UpdateItemRequest request)
    {
        try
        {
            _logger.LogInformation($"Updating item with ID: {id}");

            var item = await _context.Items.FindAsync(id);
            if (item == null)
            {
                _logger.LogWarning($"Item with ID {id} not found");
                return ApiResponse<ItemResponse>.FailureResponse("Item not found");
            }

            // Validate duplicate name if it's changing
            if (request.ItemName != null && !request.ItemName.Equals(item.ItemName, StringComparison.OrdinalIgnoreCase))
            {
                var normalizedName = request.ItemName.Trim().ToLower();
                var nameExists = await _context.Items.AnyAsync(i => i.ItemName.Trim().ToLower() == normalizedName && i.ItemId != id);
                if (nameExists)
                {
                    return ApiResponse<ItemResponse>.FailureResponse("An item with this name already exists.");
                }
            }

            // Validate references if they're being updated
            if (request.UomId.HasValue)
            {
                var uomExists = await _context.UnitOfMeasures.AnyAsync(u => u.UomId == request.UomId.Value);
                if (!uomExists)
                {
                    _logger.LogWarning($"Invalid UOM ID: {request.UomId.Value}");
                    return ApiResponse<ItemResponse>.FailureResponse("Invalid UOM ID");
                }
                item.UomId = request.UomId.Value;
            }

            if (request.CategoryId.HasValue)
            {
                var categoryExists = await _context.Categories.AnyAsync(c => c.CategoryId == request.CategoryId.Value);
                if (!categoryExists)
                {
                    _logger.LogWarning($"Invalid Category ID: {request.CategoryId.Value}");
                    return ApiResponse<ItemResponse>.FailureResponse("Invalid Category ID");
                }
                item.CategoryId = request.CategoryId.Value;
            }

            if (request.ItemName != null)
                item.ItemName = request.ItemName;
            if (request.MinStockLevel.HasValue)
                item.MinStockLevel = request.MinStockLevel.Value;
            if (request.MaxStockLevel.HasValue)
                item.MaxStockLevel = request.MaxStockLevel.Value;
            if (request.IsActive.HasValue)
                item.IsActive = request.IsActive.Value;

            _context.Items.Update(item);
            await _context.SaveChangesAsync();

            await _context.Entry(item).Reference(i => i.Uom).LoadAsync();
            await _context.Entry(item).Reference(i => i.Category).LoadAsync();

            var currentStock = await _context.Inventories.AsNoTracking()
                .Where(inv => inv.ItemId == id)
                .SumAsync(inv => (int?)inv.CurrentStock) ?? 0;

            var response = new ItemResponse
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                UomId = item.UomId,
                CategoryId = item.CategoryId,
                MinStockLevel = item.MinStockLevel,
                MaxStockLevel = item.MaxStockLevel,
                IsActive = item.IsActive,
                UomName = item.Uom!.Name,
                CategoryName = item.Category!.CategoryName,
                CurrentStock = currentStock
            };

            _logger.LogInformation($"Item with ID {id} updated successfully");
            return ApiResponse<ItemResponse>.SuccessResponse(response, "Item updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating item: {ex.Message}");
            return ApiResponse<ItemResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<EmptyPayload>> DeleteItemAsync(int id)
    {
        try
        {
            _logger.LogInformation($"Deleting item with ID: {id}");

            var item = await _context.Items.FindAsync(id);
            if (item == null)
            {
                _logger.LogWarning($"Item with ID {id} not found");
                return ApiResponse<EmptyPayload>.FailureResponse("Item not found");
            }

            _context.Items.Remove(item);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Item with ID {id} deleted successfully");
            return ApiResponse<EmptyPayload>.SuccessResponse(new EmptyPayload(), "Item deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting item: {ex.Message}");
            return ApiResponse<EmptyPayload>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }
}