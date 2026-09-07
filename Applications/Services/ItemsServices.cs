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
    private readonly IAuditTrail _audit;
    private readonly IDocumentNumberService _documentNumberService;

    public ItemService(ScmDbContext context, ILogger<ItemService> logger, IAuditTrail audit, IDocumentNumberService documentNumberService)
    {
        _context = context;
        _logger = logger;
        _audit = audit;
        _documentNumberService = documentNumberService;
    }

    /// <summary>
    /// Records an item change against the acting user.
    /// </summary>
    /// <remarks>
    /// This used to hardcode <c>UserId = 1</c> and stuff the literal string "scmsuser" into the
    /// FieldName column, so the audit trail asserted an identity that was never checked and misused a
    /// column meant for the field that changed.
    /// </remarks>
    private async Task LogActionAsync(string action, string itemName)
    {
        _audit.Record(nameof(Item), itemName, action);
        await _context.SaveChangesAsync();
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
                    ItemCode = i.ItemCode,
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
                        bool codeMatch = !string.IsNullOrEmpty(item.ItemCode) && item.ItemCode.ToLowerInvariant().Contains(lowerTerm);
                        bool idMatch = item.ItemId.ToString().Contains(lowerTerm);
                        bool nameMatch = item.ItemName.ToLowerInvariant().Contains(lowerTerm);
                        bool categoryMatch = item.CategoryName.ToLowerInvariant().Contains(lowerTerm);
                        bool quantityMatch = item.CurrentStock.ToString().Contains(lowerTerm);
                        return codeMatch || idMatch || nameMatch || categoryMatch || quantityMatch;
                    });
                }).ToList();
            }

            // Sort order: if 'desc' sort by ItemId desc; default sort is chronological by ItemId asc (newest at bottom)
            if (string.Equals(sort, "desc", StringComparison.OrdinalIgnoreCase))
            {
                items = items.OrderByDescending(i => i.ItemId).ToList();
            }
            else
            {
                items = items.OrderBy(i => i.ItemId).ToList();
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
                ItemCode = item.ItemCode,
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

            if (string.IsNullOrWhiteSpace(request.ItemName))
            {
                return ApiResponse<ItemResponse>.FailureResponse("Supply item name is required.");
            }

            var trimmedName = request.ItemName.Trim();
            if (trimmedName.Length > 50)
            {
                return ApiResponse<ItemResponse>.FailureResponse("Supply item name cannot exceed 50 characters.");
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedName, @"^[a-zA-Z\s]+$"))
            {
                return ApiResponse<ItemResponse>.FailureResponse("Supply item name can only contain letters.");
            }

            if (request.MinStockLevel < 1)
            {
                return ApiResponse<ItemResponse>.FailureResponse("Min stock level must be at least 1.");
            }

            if (request.MaxStockLevel < 1)
            {
                return ApiResponse<ItemResponse>.FailureResponse("Max stock level must be at least 1.");
            }

            if (request.MaxStockLevel < request.MinStockLevel)
            {
                return ApiResponse<ItemResponse>.FailureResponse("Max stock level cannot be less than min stock level.");
            }

            // Validate duplicate name against both active and inactive items
            var normalizedName = trimmedName.ToLower();
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

            var itemCode = await _documentNumberService.NextAsync(Domains.Enums.DocumentType.Item);
            
            var item = new Item
            {
                ItemCode = itemCode,
                ItemName = request.ItemName,
                UomId = request.UomId,
                // Stock is held in the unit the item was defined with. A separate purchasing unit
                // (a 50 kg sack, say) arrives with the supplier catalogue in Task 12 and is
                // converted into this unit on receipt.
                StockUomId = request.UomId,
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
                ItemCode = item.ItemCode,
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

            await LogActionAsync("Added New Supply", item.ItemName);

            _logger.LogInformation($"Item created successfully with ID: {item.ItemId}");
            return ApiResponse<ItemResponse>.SuccessResponse(response, "Item created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating item: {ex.Message}");
            return ApiResponse<ItemResponse>.FailureResponse($"An error occurred: {ex.InnerException?.Message ?? ex.Message}");
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
                var trimmedName = request.ItemName.Trim();
                if (string.IsNullOrWhiteSpace(trimmedName))
                {
                    return ApiResponse<ItemResponse>.FailureResponse("Supply item name cannot be empty.");
                }
                if (trimmedName.Length > 50)
                {
                    return ApiResponse<ItemResponse>.FailureResponse("Supply item name cannot exceed 50 characters.");
                }

                if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedName, @"^[a-zA-Z\s]+$"))
                {
                    return ApiResponse<ItemResponse>.FailureResponse("Supply item name can only contain letters.");
                }

                var normalizedName = trimmedName.ToLower();
                var nameExists = await _context.Items.AnyAsync(i => i.ItemName.Trim().ToLower() == normalizedName && i.ItemId != id);
                if (nameExists)
                {
                    return ApiResponse<ItemResponse>.FailureResponse("An item with this name already exists.");
                }
            }

            var nextMin = request.MinStockLevel ?? item.MinStockLevel;
            var nextMax = request.MaxStockLevel ?? item.MaxStockLevel;

            if (request.MinStockLevel.HasValue && request.MinStockLevel.Value < 1)
            {
                return ApiResponse<ItemResponse>.FailureResponse("Min stock level must be at least 1.");
            }
            if (request.MaxStockLevel.HasValue && request.MaxStockLevel.Value < 1)
            {
                return ApiResponse<ItemResponse>.FailureResponse("Max stock level must be at least 1.");
            }
            if (nextMax < nextMin)
            {
                return ApiResponse<ItemResponse>.FailureResponse("Max stock level cannot be less than min stock level.");
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
                // Changing the unit an item is measured in changes what its balance means, so the
                // stocking unit follows the display unit. Existing balances are NOT restated: that
                // needs a deliberate conversion, which belongs with cycle counting in Task 44.
                item.UomId = request.UomId.Value;
                item.StockUomId = request.UomId.Value;
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
                ItemCode = item.ItemCode,
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

            await LogActionAsync(request.IsActive.HasValue && !request.IsActive.Value ? "Deactivated Supply" : "Updated Supply", item.ItemName);

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

            await LogActionAsync("Deleted Supply", item.ItemName);

            _logger.LogInformation($"Item with ID {id} deleted successfully");
            return ApiResponse<EmptyPayload>.SuccessResponse(new EmptyPayload(), "Item deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting item: {ex.Message}");
            return ApiResponse<EmptyPayload>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<EmptyPayload>> ResetSupplyItemsAsync()
    {
        try
        {
            _logger.LogInformation("Resetting all supply items and document sequences");

            await _context.Database.ExecuteSqlRawAsync(@"
                TRUNCATE TABLE 
                    ""SupplierItems"", 
                    ""RecipeIngredients"", 
                    ""BatchConsumptions"", 
                    ""InventoryMovementLogs"", 
                    ""Inventories"", 
                    ""PurchaseOrderItems"", 
                    ""Items"" 
                RESTART IDENTITY CASCADE;

                DELETE FROM ""DocumentSequences"" WHERE ""DocType"" IN ('Item', 'SPL', 'item');
            ");

            return ApiResponse<EmptyPayload>.SuccessResponse(new EmptyPayload(), "Supply items reset successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error resetting supply items: {ex.Message}");
            return ApiResponse<EmptyPayload>.FailureResponse($"An error occurred while resetting items: {ex.Message}");
        }
    }
}