using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Applications.Services;

public class MrpService : IMrpService
{
    private readonly ScmDbContext _context;
    private readonly ILogger<MrpService> _logger;

    public MrpService(ScmDbContext context, ILogger<MrpService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<MrpPlanResponse>> GeneratePlanAsync(GenerateMrpPlanRequest request)
    {
        try
        {
            var grossRequirements = new Dictionary<int, decimal>();

            // 1. Calculate Gross Material Requirements from planned production runs
            if (request.PlannedProductions != null && request.PlannedProductions.Any())
            {
                foreach (var plan in request.PlannedProductions)
                {
                    var recipe = await _context.Recipes
                        .Include(r => r.RecipeIngredients)
                        .FirstOrDefaultAsync(r => r.RecipeId == plan.RecipeId);

                    if (recipe == null) continue;

                    foreach (var ingredient in recipe.RecipeIngredients)
                    {
                        var needed = ingredient.StandardQuantity * plan.PlannedBatchesCount;
                        if (grossRequirements.ContainsKey(ingredient.ItemId))
                        {
                            grossRequirements[ingredient.ItemId] += needed;
                        }
                        else
                        {
                            grossRequirements[ingredient.ItemId] = needed;
                        }
                    }
                }
            }

            // Include all inventory items
            var allItems = await _context.Items
                .Include(i => i.Category)
                .Include(i => i.Uom)
                .Include(i => i.SupplierItems).ThenInclude(si => si.Supplier)
                .ToListAsync();

            var openPos = await _context.PurchaseOrderItems
                .Include(poi => poi.PurchaseOrder)
                .Where(poi => poi.PurchaseOrder.Status == PurchaseOrderStatus.Pending)
                .ToListAsync();

            var onHandStocks = await _context.Inventories
                .GroupBy(i => i.ItemId)
                .Select(g => new { ItemId = g.Key, TotalStock = g.Sum(i => i.CurrentStock) })
                .ToDictionaryAsync(k => k.ItemId, v => v.TotalStock);

            var materialRequirements = new List<MrpMaterialRequirementDto>();
            var suggestions = new List<ReorderSuggestionDto>();

            foreach (var item in allItems)
            {
                var grossReq = grossRequirements.GetValueOrDefault(item.ItemId, 0m);
                var onHand = onHandStocks.GetValueOrDefault(item.ItemId, 0m);

                var onOrder = openPos
                    .Where(poi => poi.ItemId == item.ItemId)
                    .Sum(poi => poi.PoItemQuantity);

                var safetyStock = item.MinStockLevel;
                var netRequirement = Math.Max(0m, (grossReq + safetyStock) - (onHand + onOrder));

                materialRequirements.Add(new MrpMaterialRequirementDto
                {
                    ItemId = item.ItemId,
                    ItemName = item.ItemName,
                    CategoryName = item.Category?.CategoryName ?? "Uncategorized",
                    GrossRequirement = grossReq,
                    CurrentStockOnHand = onHand,
                    OnOrderQuantity = onOrder,
                    SafetyStockThreshold = safetyStock,
                    NetRequirement = netRequirement,
                    UomName = item.Uom?.Name ?? "pcs"
                });

                if (netRequirement > 0m || onHand <= item.MinStockLevel)
                {
                    var suggestedQty = netRequirement > 0m
                        ? netRequirement
                        : (item.MaxStockLevel > onHand ? item.MaxStockLevel - onHand : (item.MinStockLevel * 2));

                    var prefSupplier = item.SupplierItems.OrderByDescending(si => si.IsPreferred).FirstOrDefault();
                    var unitCost = prefSupplier?.UnitPrice ?? 0m;

                    var urgency = onHand <= safetyStock ? "Critical" : (onHand <= item.MinStockLevel ? "High" : "Normal");

                    suggestions.Add(new ReorderSuggestionDto
                    {
                        ItemId = item.ItemId,
                        ItemName = item.ItemName,
                        SuggestedOrderQuantity = Math.Max(suggestedQty, 1m),
                        UomName = item.Uom?.Name ?? "pcs",
                        PreferredSupplierId = prefSupplier?.SupplierId,
                        PreferredSupplierName = prefSupplier?.Supplier?.CompanyName ?? "No preferred vendor",
                        EstimatedUnitCost = unitCost,
                        EstimatedTotalCost = Math.Max(suggestedQty, 1m) * unitCost,
                        UrgencyLevel = urgency
                    });
                }
            }

            var response = new MrpPlanResponse
            {
                GeneratedAt = DateTime.UtcNow,
                PlanningHorizonDays = request.PlanningHorizonDays,
                MaterialRequirements = materialRequirements.Where(m => m.GrossRequirement > 0m || m.NetRequirement > 0m || m.CurrentStockOnHand > 0m).ToList(),
                SuggestedReorders = suggestions.OrderByDescending(s => s.UrgencyLevel == "Critical").ThenByDescending(s => s.UrgencyLevel == "High").ToList()
            };

            return ApiResponse<MrpPlanResponse>.SuccessResponse(
                response,
                $"MRP Plan generated. Identified {suggestions.Count} purchase reorder suggestions.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating MRP plan");
            return ApiResponse<MrpPlanResponse>.FailureResponse($"Failed to generate MRP plan: {ex.Message}");
        }
    }
}
