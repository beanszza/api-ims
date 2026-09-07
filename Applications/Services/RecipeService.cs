using System;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Domains.Entities;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Applications.Interfaces;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class RecipeService : IRecipeService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<RecipeService> _logger;
    private readonly IUomConversionService _uomConversion;
    private readonly IDocumentNumberService _documentNumberService;

    public RecipeService(
        ScmDbContext context,
        ILogger<RecipeService> logger,
        IUomConversionService uomConversion,
        IDocumentNumberService documentNumberService)
    {
        _context = context;
        _logger = logger;
        _uomConversion = uomConversion;
        _documentNumberService = documentNumberService;
    }

    /// <summary>
    /// Returns an explanation if an ingredient's unit cannot be converted into the unit its item is
    /// stocked in, otherwise null.
    /// </summary>
    /// <remarks>
    /// Rejecting this at save time is the point: a recipe measuring ube in litres cannot be costed,
    /// planned, or consumed, and catching it here means production never has to guess.
    /// </remarks>
    private async Task<string?> DescribeUnitMismatchAsync(int itemId, int uomId)
    {
        var item = await _context.Items
            .AsNoTracking()
            .Include(i => i.StockUom)
            .FirstOrDefaultAsync(i => i.ItemId == itemId);

        if (item is null)
        {
            return $"Item with ID {itemId} not found.";
        }

        if (await _uomConversion.CanConvertAsync(uomId, item.StockUomId))
        {
            return null;
        }

        var ingredientUom = await _context.UnitOfMeasures.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UomId == uomId);

        return $"'{item.ItemName}' is stocked in {item.StockUom?.Abbreviation ?? "an unknown unit"}, " +
               $"which cannot be converted from {ingredientUom?.Abbreviation ?? $"unit {uomId}"}. " +
               "Choose a unit that measures the same thing.";
    }

    public async Task<ApiResponse<IEnumerable<RecipeResponse>>> GetAllRecipesAsync()
    {
        try
        {
            var recipes = await _context.Recipes
                .Include(r => r.RecipeIngredients)
                    .ThenInclude(ri => ri.Item)
                .Include(r => r.RecipeIngredients)
                    .ThenInclude(ri => ri.Uom)
                .Include(r => r.Product)
                .Select(r => new RecipeResponse
                {
                    RecipeId = r.RecipeId,
                    RecipeCode = r.RecipeCode,
                    RecipeName = r.RecipeName,
                    ProductId = r.ProductId,
                    OutputQuantity = r.OutputQuantity,
                    Notes = r.Notes,
                    IsActive = r.IsActive,
                    Ingredients = r.RecipeIngredients.Select(ri => new RecipeIngredientResponse
                    {
                        IngredientId = ri.IngredientId,
                        ItemId = ri.ItemId,
                        UomId = ri.UomId,
                        StandardQuantity = ri.StandardQuantity
                    }).ToList()
                })
                .ToListAsync();

            return ApiResponse<IEnumerable<RecipeResponse>>.SuccessResponse(recipes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recipes.");
            return ApiResponse<IEnumerable<RecipeResponse>>.FailureResponse("An error occurred while fetching recipes.");
        }
    }

    public async Task<ApiResponse<RecipeResponse>> CreateRecipeAsync(CreateRecipeRequest request)
    {
        try
        {
            _logger.LogInformation("Creating new recipe for product ID {ProductId}", request.ProductId);

            // Validate Product
            var product = await _context.FinishedProducts.FirstOrDefaultAsync(p => p.ProductId == request.ProductId);
            if (product == null)
            {
                return ApiResponse<RecipeResponse>.FailureResponse($"Product with ID {request.ProductId} not found.");
            }

            // Validate Ingredient Quantities
            if (request.Ingredients == null || !request.Ingredients.Any())
            {
                return ApiResponse<RecipeResponse>.FailureResponse("Recipe must have at least one ingredient.");
            }

            foreach (var ing in request.Ingredients)
            {
                if (ing.ItemId == product.ItemId)
                {
                    return ApiResponse<RecipeResponse>.FailureResponse("A finished product cannot be an ingredient of its own recipe.");
                }
                if (ing.StandardQuantity <= 0)
                {
                    return ApiResponse<RecipeResponse>.FailureResponse("Ingredient quantities must be greater than zero.");
                }

                var unitProblem = await DescribeUnitMismatchAsync(ing.ItemId, ing.UomId);
                if (unitProblem is not null)
                {
                    return ApiResponse<RecipeResponse>.FailureResponse(unitProblem);
                }
            }

            var recipeCode = await _documentNumberService.NextAsync(Domains.Enums.DocumentType.Recipe);

            var recipe = new Recipe
            {
                RecipeCode = recipeCode,
                RecipeName = request.RecipeName,
                ProductId = request.ProductId,
                OutputQuantity = request.OutputQuantity,
                Notes = request.Notes,
                IsActive = request.IsActive,
                RecipeIngredients = request.Ingredients.Select(i => new RecipeIngredient
                {
                    ItemId = i.ItemId,
                    UomId = i.UomId,
                    StandardQuantity = i.StandardQuantity
                }).ToList()
            };

            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync();

            try
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    EntityName = "Recipe",
                    EntityId = recipe.RecipeName,
                    Action = "Recipe/BOM Created & Registered",
                    FieldName = "Production Supervisor",
                    OldValue = "0",
                    NewValue = $"{recipe.OutputQuantity} units",
                    Timestamp = DateTime.UtcNow,
                    UserId = Domains.Identity.SystemUsers.System,
                    UserName = "Kitchen Manager"
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning("Failed to write audit log for recipe create: {Msg}", auditEx.Message);
            }

            var response = new RecipeResponse
            {
                RecipeId = recipe.RecipeId,
                RecipeCode = recipe.RecipeCode,
                RecipeName = recipe.RecipeName,
                ProductId = recipe.ProductId,
                OutputQuantity = recipe.OutputQuantity,
                Notes = recipe.Notes,
                IsActive = recipe.IsActive,
                Ingredients = recipe.RecipeIngredients.Select(i => new RecipeIngredientResponse
                {
                    IngredientId = i.IngredientId,
                    ItemId = i.ItemId,
                    UomId = i.UomId,
                    StandardQuantity = i.StandardQuantity
                }).ToList()
            };

            _logger.LogInformation("Recipe created successfully with ID {RecipeId}", recipe.RecipeId);
            return ApiResponse<RecipeResponse>.SuccessResponse(response, "Recipe created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating recipe: {Message}", ex.Message);
            return ApiResponse<RecipeResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<RecipeResponse>> UpdateRecipeAsync(int id, UpdateRecipeRequest request)
    {
        try
        {
            _logger.LogInformation("Updating recipe with ID {RecipeId}", id);

            var recipe = await _context.Recipes
                .Include(r => r.RecipeIngredients)
                .FirstOrDefaultAsync(r => r.RecipeId == id);

            if (recipe == null)
            {
                return ApiResponse<RecipeResponse>.FailureResponse($"Recipe with ID {id} not found.");
            }

            if (request.RecipeName != null)
            {
                recipe.RecipeName = request.RecipeName;
            }

            if (request.OutputQuantity.HasValue)
            {
                if (request.OutputQuantity.Value <= 0)
                {
                    return ApiResponse<RecipeResponse>.FailureResponse("Output quantity must be greater than zero.");
                }
                recipe.OutputQuantity = request.OutputQuantity.Value;
            }

            if (request.Notes != null)
            {
                recipe.Notes = request.Notes;
            }

            if (request.IsActive.HasValue)
            {
                recipe.IsActive = request.IsActive.Value;
            }

            if (request.Ingredients != null)
            {
                // Validate ingredient existences, quantities, and self-reference
                foreach (var ing in request.Ingredients)
                {
                    if (ing.ItemId == recipe.Product?.ItemId)
                    {
                        return ApiResponse<RecipeResponse>.FailureResponse("A finished product cannot be an ingredient of its own recipe.");
                    }
                    if (ing.StandardQuantity <= 0)
                    {
                        return ApiResponse<RecipeResponse>.FailureResponse("Ingredient quantities must be greater than zero.");
                    }
                    var itemExists = await _context.Items.AnyAsync(i => i.ItemId == ing.ItemId);
                    if (!itemExists)
                    {
                        return ApiResponse<RecipeResponse>.FailureResponse($"Ingredient Item with ID {ing.ItemId} does not exist.");
                    }
                    var uomExists = await _context.UnitOfMeasures.AnyAsync(u => u.UomId == ing.UomId);
                    if (!uomExists)
                    {
                        return ApiResponse<RecipeResponse>.FailureResponse($"Unit of Measure with ID {ing.UomId} does not exist.");
                    }

                    var unitProblem = await DescribeUnitMismatchAsync(ing.ItemId, ing.UomId);
                    if (unitProblem is not null)
                    {
                        return ApiResponse<RecipeResponse>.FailureResponse(unitProblem);
                    }
                }

                // Diff update of ingredients:
                // 1. Remove ingredients not in request
                var reqItemIds = request.Ingredients.Select(i => i.ItemId).ToList();
                var toRemove = recipe.RecipeIngredients.Where(ri => !reqItemIds.Contains(ri.ItemId)).ToList();
                foreach (var ri in toRemove)
                {
                    recipe.RecipeIngredients.Remove(ri);
                }

                // 2. Add new ones or update existing ones
                foreach (var ing in request.Ingredients)
                {
                    var existing = recipe.RecipeIngredients.FirstOrDefault(ri => ri.ItemId == ing.ItemId);
                    if (existing != null)
                    {
                        existing.StandardQuantity = ing.StandardQuantity;
                        existing.UomId = ing.UomId;
                    }
                    else
                    {
                        recipe.RecipeIngredients.Add(new RecipeIngredient
                        {
                            ItemId = ing.ItemId,
                            UomId = ing.UomId,
                            StandardQuantity = ing.StandardQuantity
                        });
                    }
                }
            }

            _context.Recipes.Update(recipe);
            await _context.SaveChangesAsync();

            try
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    EntityName = "Recipe",
                    EntityId = recipe.RecipeName,
                    Action = recipe.IsActive ? "Recipe/BOM Updated & Active" : "Recipe/BOM Status Changed to Inactive",
                    FieldName = "Production Supervisor",
                    OldValue = "Updated",
                    NewValue = $"{recipe.OutputQuantity} units",
                    Timestamp = DateTime.UtcNow,
                    UserId = Domains.Identity.SystemUsers.System,
                    UserName = "Kitchen Manager"
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning("Failed to write audit log for recipe update: {Msg}", auditEx.Message);
            }

            var response = new RecipeResponse
            {
                RecipeId = recipe.RecipeId,
                RecipeCode = recipe.RecipeCode,
                RecipeName = recipe.RecipeName,
                ProductId = recipe.ProductId,
                OutputQuantity = recipe.OutputQuantity,
                Notes = recipe.Notes,
                IsActive = recipe.IsActive,
                Ingredients = recipe.RecipeIngredients.Select(i => new RecipeIngredientResponse
                {
                    IngredientId = i.IngredientId,
                    ItemId = i.ItemId,
                    UomId = i.UomId,
                    StandardQuantity = i.StandardQuantity
                }).ToList()
            };

            _logger.LogInformation("Recipe with ID {RecipeId} updated successfully", recipe.RecipeId);
            return ApiResponse<RecipeResponse>.SuccessResponse(response, "Recipe updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error updating recipe: {Message}", ex.Message);
            return ApiResponse<RecipeResponse>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }

    public async Task<ApiResponse<EmptyPayload>> DeleteRecipeAsync(int id)
    {
        try
        {
            _logger.LogInformation("Deleting recipe with ID {RecipeId}", id);
            var recipe = await _context.Recipes
                .Include(r => r.RecipeIngredients)
                .FirstOrDefaultAsync(r => r.RecipeId == id);

            if (recipe == null)
            {
                return ApiResponse<EmptyPayload>.FailureResponse($"Recipe with ID {id} not found.");
            }

            _context.Recipes.Remove(recipe);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Recipe with ID {RecipeId} deleted successfully", id);
            return ApiResponse<EmptyPayload>.SuccessResponse(new EmptyPayload(), "Recipe deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error deleting recipe: {Message}", ex.Message);
            return ApiResponse<EmptyPayload>.FailureResponse($"An error occurred: {ex.Message}");
        }
    }
}
