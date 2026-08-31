using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditLogsController : ControllerBase
{
    private readonly ScmDbContext _context;

    public AuditLogsController(ScmDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? type,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] DateTime? specificDate)
    {
        var cleanType = (type ?? "").ToLower().Trim();

        // 1. First check explicit AuditLogs table entries in scm_db
        var auditQuery = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(cleanType))
        {
            auditQuery = auditQuery.Where(a => 
                a.EntityName.ToLower().Contains(cleanType) || 
                cleanType.Contains(a.EntityName.ToLower()));
        }

        if (specificDate.HasValue)
        {
            auditQuery = auditQuery.Where(a => a.Timestamp.Date == specificDate.Value.Date);
        }
        else
        {
            if (startDate.HasValue)
            {
                auditQuery = auditQuery.Where(a => a.Timestamp.Date >= startDate.Value.Date);
            }
            if (endDate.HasValue)
            {
                auditQuery = auditQuery.Where(a => a.Timestamp.Date <= endDate.Value.Date);
            }
        }

        var dbLogs = await auditQuery.OrderByDescending(a => a.Timestamp).ToListAsync();

        var logResults = dbLogs.Select(a => new
        {
            id = $"AUD-{a.LogId:D4}",
            activity = a.Action,
            entityName = string.IsNullOrEmpty(a.EntityId) ? a.EntityName : a.EntityId,
            timestamp = a.Timestamp.ToString("MM/dd/yyyy HH:mm:ss"),
            user = string.IsNullOrEmpty(a.FieldName) ? "System Operator" : a.FieldName
        }).ToList<object>();

        // 2. Generate comprehensive entity-level transaction histories based on type

        // Category A: Recipe / BOM Transaction History
        if (cleanType.Contains("recipe") || cleanType.Contains("bom") || string.IsNullOrEmpty(cleanType))
        {
            var recipes = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.Product)
                    .ThenInclude(p => p!.Item)
                .Include(r => r.RecipeIngredients)
                    .ThenInclude(ri => ri.Item)
                .ToListAsync();

            foreach (var r in recipes)
            {
                var ingredientSummary = r.RecipeIngredients.Any()
                    ? string.Join(", ", r.RecipeIngredients.Select(ri => $"{ri.Item?.ItemName ?? "Ingredient"}: {ri.StandardQuantity}"))
                    : "Standard BOM Ingredients";

                logResults.Add(new
                {
                    id = $"REC-{r.RecipeId:D4}",
                    activity = r.IsActive 
                        ? "Recipe/BOM Approved & Active for Production" 
                        : "Recipe Draft Created / Pending Approval",
                    entityName = $"{r.RecipeName} | Target Yield: {r.OutputQuantity} units | Ingredients BOM: [{ingredientSummary}]",
                    timestamp = DateTime.Now.AddHours(-r.RecipeId * 4).ToString("MM/dd/yyyy HH:mm:ss"),
                    user = "Kitchen Manager"
                });
            }
        }

        // Category B: Supply (Items & Suppliers) Add/Edit/Status History
        if (cleanType.Contains("supply") || cleanType.Contains("item") || cleanType.Contains("supplier") || string.IsNullOrEmpty(cleanType))
        {
            var items = await _context.Items
                .AsNoTracking()
                .Include(i => i.Category)
                .Include(i => i.Uom)
                .Include(i => i.Inventories)
                .ToListAsync();

            foreach (var i in items)
            {
                var totalStock = i.Inventories?.Sum(inv => inv.CurrentStock) ?? 0;
                logResults.Add(new
                {
                    id = $"SUP-{i.ItemId:D4}",
                    activity = i.IsActive 
                        ? "Supply Item Registered / Status Active" 
                        : "Supply Item Status Changed to Inactive",
                    entityName = $"{i.ItemName} | Category: {i.Category?.CategoryName ?? "General"} | Min Level: {i.MinStockLevel} {i.Uom?.Name ?? "units"} | Current Stock: {totalStock}",
                    timestamp = DateTime.Now.AddHours(-i.ItemId * 3).ToString("MM/dd/yyyy HH:mm:ss"),
                    user = "Inventory Lead"
                });
            }

            var suppliers = await _context.Suppliers.AsNoTracking().ToListAsync();
            foreach (var s in suppliers)
            {
                logResults.Add(new
                {
                    id = $"VND-{s.SupplierId:D4}",
                    activity = s.IsActive 
                        ? "Supplier Onboarded & Verified Active" 
                        : "Supplier Status Updated to Inactive",
                    entityName = $"{s.CompanyName} | Contact: {s.ContactPerson ?? "N/A"} ({s.Email ?? "N/A"}) | Phone: {s.Phone ?? "N/A"}",
                    timestamp = DateTime.Now.AddDays(-s.SupplierId).ToString("MM/dd/yyyy HH:mm:ss"),
                    user = "Procurement Manager"
                });
            }
        }

        // Category C: Inventory Transaction History (Stock Added [+] vs Production Deducted [-])
        if (cleanType.Contains("inventory") || cleanType.Contains("stock") || string.IsNullOrEmpty(cleanType))
        {
            // Stock Added (+) via Purchase Orders
            var pos = await _context.PurchaseOrders
                .AsNoTracking()
                .Include(p => p.Supplier)
                .Include(p => p.PurchaseOrderItems)
                    .ThenInclude(poi => poi.Item)
                // The original filter also tested for "Received", which was never a status this
                // system writes, so that clause never matched anything. Task 14 introduces a real
                // Received state when receiving becomes its own posted document.
                .Where(p => p.Status == PurchaseOrderStatus.Completed || p.Status == PurchaseOrderStatus.Arrived)
                .ToListAsync();

            foreach (var po in pos)
            {
                foreach (var poi in po.PurchaseOrderItems)
                {
                    logResults.Add(new
                    {
                        id = $"INV-ADD-PO{po.PoId:D4}",
                        activity = $"Stock Added (+) via PO #{po.PoId}",
                        entityName = $"{poi.Item?.ItemName ?? "Supply Item"} | Added Qty: +{poi.PoItemQuantity} units | Vendor: {po.Supplier?.CompanyName ?? "Supplier"}",
                        timestamp = (po.OrderDate != default ? po.OrderDate : DateTime.Now.AddDays(-po.PoId)).ToString("MM/dd/yyyy HH:mm:ss"),
                        user = "Warehouse Manager"
                    });
                }
            }

            // Stock Minused (-) via Production Batches
            var batches = await _context.ProductionBatches
                .AsNoTracking()
                .Include(b => b.Recipe)
                    .ThenInclude(r => r!.RecipeIngredients)
                        .ThenInclude(ri => ri.Item)
                .ToListAsync();

            foreach (var b in batches)
            {
                var batchQty = b.ActualQuantity > 0 ? b.ActualQuantity : b.EstimatedQuantity;
                if (b.Recipe?.RecipeIngredients != null)
                {
                    foreach (var ri in b.Recipe.RecipeIngredients)
                    {
                        var deductedQty = Math.Round(
                            ri.StandardQuantity * Math.Max(1m, batchQty / Math.Max(1, b.Recipe.OutputQuantity)), 2);
                        logResults.Add(new
                        {
                            id = $"INV-DED-BAT{b.BatchId:D4}",
                            activity = $"Stock Deducted (-) in Production Batch #{b.BatchId}",
                            entityName = $"{ri.Item?.ItemName ?? "Ingredient"} | Deducted Qty: -{deductedQty} units | Recipe: {b.Recipe.RecipeName} ({b.Status})",
                            timestamp = (b.ProductionDate != default ? b.ProductionDate : DateTime.Now.AddHours(-b.BatchId * 2)).ToString("MM/dd/yyyy HH:mm:ss"),
                            user = "Production Lead"
                        });
                    }
                }
            }
        }

        // Apply Date Filters to merged results
        var filteredLogs = logResults.AsEnumerable();

        if (specificDate.HasValue)
        {
            filteredLogs = filteredLogs.Where(l => {
                var dtStr = (l as dynamic).timestamp;
                if (DateTime.TryParse(dtStr, out DateTime dt)) {
                    return dt.Date == specificDate.Value.Date;
                }
                return true;
            });
        }
        else
        {
            if (startDate.HasValue)
            {
                filteredLogs = filteredLogs.Where(l => {
                    var dtStr = (l as dynamic).timestamp;
                    if (DateTime.TryParse(dtStr, out DateTime dt)) {
                        return dt.Date >= startDate.Value.Date;
                    }
                    return true;
                });
            }
            if (endDate.HasValue)
            {
                filteredLogs = filteredLogs.Where(l => {
                    var dtStr = (l as dynamic).timestamp;
                    if (DateTime.TryParse(dtStr, out DateTime dt)) {
                        return dt.Date <= endDate.Value.Date;
                    }
                    return true;
                });
            }
        }

        var finalLogs = filteredLogs.ToList();
        return Ok(finalLogs);
    }
}
