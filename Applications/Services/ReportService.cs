using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Api.Contracts.Requests;
using api_scm.Api.Contracts.Responses;
using api_scm.Contracts.Responses;
using Applications.Interfaces;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Domains.Entities;
using Domains.Enums;

namespace Applications.Services;

public class ReportService : IReportService
{
    /// <summary>
    /// A batch counts as QA-passed if QA explicitly approved it, or if it has moved past QA into
    /// packaging, completion, or stock. Kept in one place because four separate reports used to
    /// repeat the same four-way string comparison, and they had already started to drift.
    /// </summary>
    private static bool IsQaPassed(ProductionBatch batch) =>
        batch.QualityStatus == QcStatus.Approved
        || batch.Status is BatchStatus.PassedQa or BatchStatus.InventoryAdded or BatchStatus.Completed;

    private static bool IsQaRejected(ProductionBatch batch) =>
        batch.QualityStatus == QcStatus.Rejected || batch.Status == BatchStatus.Rejected;

    private readonly ScmDbContext _context;
    private readonly ILogger<ReportService> _logger;

    public ReportService(ScmDbContext context, ILogger<ReportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private bool IsDateInRange(DateTime date, ReportFilterDto filter)
    {
        if (filter.StartDate.HasValue && date < filter.StartDate.Value) return false;
        if (filter.EndDate.HasValue && date > filter.EndDate.Value) return false;
        if (filter.Month.HasValue && date.Month != filter.Month.Value) return false;
        if (filter.Year.HasValue && date.Year != filter.Year.Value) return false;
        return true;
    }

    public async Task<ApiResponse<InventoryReportResponseDto>> GetInventoryReportAsync(ReportFilterDto filter)
    {
        try
        {
            var logsQuery = _context.InventoryMovementLogs
                .AsNoTracking()
                .Include(log => log.Item)
                .AsQueryable();

            if (filter != null)
            {
                if (filter.StartDate.HasValue)
                    logsQuery = logsQuery.Where(log => log.Timestamp >= filter.StartDate.Value.ToUniversalTime());
                if (filter.EndDate.HasValue)
                    logsQuery = logsQuery.Where(log => log.Timestamp <= filter.EndDate.Value.ToUniversalTime());
                if (filter.Month.HasValue)
                    logsQuery = logsQuery.Where(log => log.Timestamp.Month == filter.Month.Value);
                if (filter.Year.HasValue)
                    logsQuery = logsQuery.Where(log => log.Timestamp.Year == filter.Year.Value);
            }

            var logs = await logsQuery.ToListAsync();

            var historicalAudit = logs
                .GroupBy(log => new { log.Timestamp.Year, log.Timestamp.Month })
                .OrderByDescending(g => g.Key.Year)
                .ThenByDescending(g => g.Key.Month)
                .Select(g => {
                    var period = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy");
                    var itemsCount = g.Select(l => l.ItemId).Distinct().Count();
                    
                    var stockIn = g.Where(l => l.ActionType.Contains("Add", StringComparison.OrdinalIgnoreCase) || l.ActionType.Contains("Receive", StringComparison.OrdinalIgnoreCase) || (l.ChangeQuantity > 0 && !l.ActionType.Contains("Transfer", StringComparison.OrdinalIgnoreCase)))
                                   .Sum(l => l.ChangeQuantity);
                    var stockOut = g.Where(l => l.ActionType.Contains("Deduct", StringComparison.OrdinalIgnoreCase) || l.ActionType.Contains("Consume", StringComparison.OrdinalIgnoreCase) || (l.ActionType.Contains("Transfer", StringComparison.OrdinalIgnoreCase) && l.ChangeQuantity < 0))
                                    .Sum(l => Math.Abs(l.ChangeQuantity));
                    var wastage = g.Where(l => l.ActionType.Contains("Wastage", StringComparison.OrdinalIgnoreCase) || l.ActionType.Contains("Spoil", StringComparison.OrdinalIgnoreCase) || l.ActionType.Contains("Reject", StringComparison.OrdinalIgnoreCase))
                                   .Sum(l => Math.Abs(l.ChangeQuantity));

                    return new HistoricalInventoryAuditDto
                    {
                        Period = period,
                        TotalActiveItems = $"{itemsCount} Items",
                        StartingStockQty = "N/A", // Snapshots not currently stored
                        EndingStockQty = "N/A",
                        StockInQty = $"{stockIn} units",
                        StockOutQty = $"{stockOut} units",
                        WastageQty = $"{wastage} units",
                        InventoryVelocity = stockIn > 0 ? $"{Math.Round(stockOut / stockIn * 100, 1)}%" : "0%"
                    };
                }).ToList();

            var itemsQuery = await _context.Items
                .AsNoTracking()
                .Include(i => i.Uom)
                .Include(i => i.Inventories)
                .Where(i => i.IsActive)
                .ToListAsync();

            // For Demand Forecast, we need 30-day avg daily usage
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var recentOutLogs = await _context.InventoryMovementLogs
                .AsNoTracking()
                .Where(l => l.Timestamp >= thirtyDaysAgo && l.ChangeQuantity < 0 && !l.ActionType.Contains("Transfer"))
                .ToListAsync();

            var demandForecast = itemsQuery.Select(i => {
                var currentStock = i.Inventories?.Sum(inv => inv.CurrentStock) ?? 0;
                
                var itemOutLogs = recentOutLogs.Where(l => l.ItemId == i.ItemId).ToList();
                var thirtyDayUsage = itemOutLogs.Sum(l => Math.Abs(l.ChangeQuantity));
                var avgDaily = Math.Round(thirtyDayUsage / 30m, 1);

                decimal daysLeftRaw = avgDaily > 0 ? currentStock / avgDaily : 999m;
                string daysLeftStr = daysLeftRaw > 100 ? ">100 Days" : $"{Math.Round(daysLeftRaw, 0)} Days";
                string runoutDate = daysLeftRaw > 100 ? "N/A" : DateTime.UtcNow.AddDays((double)daysLeftRaw).ToString("MMM dd, yyyy");

                string badge = "NORMAL";
                if (daysLeftRaw < 5 || currentStock == 0) badge = "CRITICAL";
                else if (daysLeftRaw < 14 || currentStock < i.MinStockLevel) badge = "WARNING";

                decimal recommended = Math.Max(0, i.MaxStockLevel - currentStock);
                string uom = i.Uom?.Abbreviation ?? "units";

                return new InventoryDemandForecastDto
                {
                    ItemName = i.ItemName,
                    CurrentStock = $"{currentStock} {uom}",
                    AvgDailyUsage = $"{avgDaily} {uom}/day",
                    DaysLeft = daysLeftStr,
                    RunoutDate = runoutDate,
                    UrgencyBadge = badge,
                    RecommendedReorderQty = $"{recommended} {uom}"
                };
            }).OrderBy(d => d.UrgencyBadge == "CRITICAL" ? 0 : d.UrgencyBadge == "WARNING" ? 1 : 2).ToList();

            var response = new InventoryReportResponseDto
            {
                HistoricalAudit = historicalAudit,
                DemandForecast = demandForecast
            };

            return ApiResponse<InventoryReportResponseDto>.SuccessResponse(response, "Inventory report generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating inventory report.");
            return ApiResponse<InventoryReportResponseDto>.FailureResponse("An error occurred while generating the inventory report.");
        }
    }

    public async Task<ApiResponse<ProcurementReportResponseDto>> GetProcurementReportAsync(ReportFilterDto filter)
    {
        try
        {
            var query = _context.PurchaseOrders
                .AsNoTracking()
                .Include(po => po.Supplier)
                .Include(po => po.PurchaseOrderItems)
                .AsQueryable();

            if (filter != null)
            {
                if (filter.StartDate.HasValue)
                    query = query.Where(po => po.OrderDate >= filter.StartDate.Value.ToUniversalTime());
                if (filter.EndDate.HasValue)
                    query = query.Where(po => po.OrderDate <= filter.EndDate.Value.ToUniversalTime());
                if (filter.Month.HasValue)
                    query = query.Where(po => po.OrderDate.Month == filter.Month.Value);
                if (filter.Year.HasValue)
                    query = query.Where(po => po.OrderDate.Year == filter.Year.Value);
            }

            var pos = await query.ToListAsync();

            var summary = new OrderFulfillmentSummaryDto
            {
                TotalOrders = pos.Count,
                PendingOrders = pos.Count(po => po.Status == PurchaseOrderStatus.Pending),
                ArrivedOrders = pos.Count(po => po.Status == PurchaseOrderStatus.Arrived),
                CompletedOrders = pos.Count(po => po.Status == PurchaseOrderStatus.Completed),
                RejectedOrders = pos.Count(po => po.Status == PurchaseOrderStatus.Rejected),
                CancelledOrders = pos.Count(po => po.Status == PurchaseOrderStatus.Cancelled)
            };

            var historicalAudit = pos.Select(po =>
            {
                var allItems = po.PurchaseOrderItems ?? new List<Domains.Entities.PurchaseOrderItem>();
                int totalItems = allItems.Count;
                double totalOrdered = allItems.Sum(i => (double)i.PoItemQuantity);
                double totalReceived = allItems.Sum(i => (double)i.ReceivedQuantity);
                
                string fulfillmentRate = totalOrdered > 0 ? $"{Math.Round((totalReceived / totalOrdered) * 100, 1)}%" : "0%";
                
                string leadTime = "Pending";
                if (po.QaInspectedDate.HasValue)
                {
                    leadTime = $"{Math.Round((po.QaInspectedDate.Value - po.OrderDate).TotalDays, 1)} Days";
                }

                return new HistoricalProcurementAuditDto
                {
                    PoId = $"PO-{po.PoId:D4}",
                    IssueDate = po.OrderDate.ToString("yyyy-MM-dd"),
                    SupplierName = po.Supplier?.CompanyName ?? "Unknown",
                    TotalItemsCount = $"{totalItems} Items",
                    TotalOrderedQty = $"{totalOrdered}",
                    DeliveryLeadTime = leadTime,
                    FulfillmentRate = fulfillmentRate,
                    InspectionStatus = string.IsNullOrWhiteSpace(po.QaStatus) ? "Pending QA" : po.QaStatus
                };
            }).OrderByDescending(h => h.IssueDate).ToList();

            var response = new ProcurementReportResponseDto
            {
                OrderFulfillmentSummary = summary,
                HistoricalAudit = historicalAudit
            };

            return ApiResponse<ProcurementReportResponseDto>.SuccessResponse(response, "Procurement report generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating procurement report.");
            return ApiResponse<ProcurementReportResponseDto>.FailureResponse("An error occurred while generating the procurement report.");
        }
    }

    public async Task<ApiResponse<ProductionQualityReportResponseDto>> GetProductionReportAsync(ReportFilterDto filter)
    {
        try
        {
            var query = _context.ProductionBatches
                .AsNoTracking()
                .Include(b => b.Recipe)
                .Include(b => b.Product)
                    .ThenInclude(p => p.Item)
                .AsQueryable();

            if (filter != null)
            {
                if (filter.StartDate.HasValue)
                    query = query.Where(b => b.ProductionDate >= filter.StartDate.Value.ToUniversalTime());
                if (filter.EndDate.HasValue)
                    query = query.Where(b => b.ProductionDate <= filter.EndDate.Value.ToUniversalTime());
                if (filter.Month.HasValue)
                    query = query.Where(b => b.ProductionDate.Month == filter.Month.Value);
                if (filter.Year.HasValue)
                    query = query.Where(b => b.ProductionDate.Year == filter.Year.Value);
            }

            var batches = await query.ToListAsync();

            int totalBatches = batches.Count;
            int scheduledBatches = batches.Count(b => b.Status == BatchStatus.Scheduled);
            int inProgressBatches = batches.Count(b => b.Status == BatchStatus.InProgress);
            int passedQaBatches = batches.Count(IsQaPassed);
            int rejectedBatches = batches.Count(IsQaRejected);

            var groupedByRecipe = batches
                .GroupBy(b => b.Recipe?.RecipeName ?? b.Product?.Item?.ItemName ?? "Unknown Recipe")
                .Select(g =>
                {
                    int cooked = g.Count();
                    decimal totalOutput = g.Sum(b => b.ActualQuantity > 0 ? b.ActualQuantity : b.EstimatedQuantity);
                    int passed = g.Count(IsQaPassed);

                    double yieldRate = cooked > 0 ? Math.Round((passed / (double)cooked) * 100, 1) : 0;
                    decimal rejectedQty = g.Where(IsQaRejected).Sum(b => b.EstimatedQuantity);

                    var topReason = g.Where(b => !string.IsNullOrWhiteSpace(b.RejectionReason))
                        .GroupBy(b => b.RejectionReason)
                        .OrderByDescending(rg => rg.Count())
                        .Select(rg => rg.Key)
                        .FirstOrDefault() ?? "None";

                    return new
                    {
                        RecipeName = g.Key,
                        CookedBatches = cooked,
                        TotalOutput = totalOutput,
                        YieldSuccessRate = yieldRate,
                        RejectedQty = rejectedQty,
                        Reason = topReason
                    };
                })
                .OrderByDescending(r => r.CookedBatches)
                .ToList();

            string mostProduced = groupedByRecipe.FirstOrDefault() != null ? $"{groupedByRecipe.First().RecipeName} ({groupedByRecipe.First().CookedBatches} Batches)" : "None";
            string seldomProduced = groupedByRecipe.LastOrDefault() != null ? $"{groupedByRecipe.Last().RecipeName} ({groupedByRecipe.Last().CookedBatches} Batches)" : "None";

            var summary = new ProductionSummaryDto
            {
                TotalBatches = totalBatches,
                ScheduledBatches = scheduledBatches,
                InProgressBatches = inProgressBatches,
                PassedQaBatches = passedQaBatches,
                RejectedBatches = rejectedBatches,
                MostProducedItem = mostProduced,
                SeldomProducedItem = seldomProduced
            };

            var yieldEfficiency = groupedByRecipe.Select(r => new KitchenYieldEfficiencyDto
            {
                RecipeName = r.RecipeName,
                TotalBatchesCooked = $"{r.CookedBatches} Batches",
                TotalOutputQty = $"{r.TotalOutput:N0} Pcs",
                YieldSuccessRate = $"{r.YieldSuccessRate:F1}%",
                TotalRejectedQty = $"{r.RejectedQty:N0} Pcs",
                IngredientWasteQty = "0 kg",
                CommonFailureReason = r.Reason
            }).ToList();

            var response = new ProductionQualityReportResponseDto
            {
                Summary = summary,
                YieldEfficiency = yieldEfficiency
            };

            return ApiResponse<ProductionQualityReportResponseDto>.SuccessResponse(response, "Production report generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating production report.");
            return ApiResponse<ProductionQualityReportResponseDto>.FailureResponse("An error occurred while generating the production report.");
        }
    }

    public async Task<ApiResponse<SupplierPerformanceReportResponseDto>> GetSupplierReportAsync(ReportFilterDto filter)
    {
        try
        {
            var suppliers = await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.IsActive)
                .Include(s => s.PurchaseOrders)
                    .ThenInclude(po => po.PurchaseOrderItems)
                .ToListAsync();

            var scorecard = new List<VendorScorecardAuditDto>();

            foreach (var supplier in suppliers)
            {
                var filteredPOs = supplier.PurchaseOrders
                    .Where(po => IsDateInRange(po.OrderDate, filter))
                    .ToList();

                if (!filteredPOs.Any()) continue;

                int totalOrders = filteredPOs.Count;
                int onTime = filteredPOs.Count(po => po.QaInspectedDate.HasValue && po.QaInspectedDate.Value.Date <= po.ExpectedArrivalDate.Date);
                int late = filteredPOs.Count(po => po.QaInspectedDate.HasValue && po.QaInspectedDate.Value.Date > po.ExpectedArrivalDate.Date);

                var allItems = filteredPOs.SelectMany(po => po.PurchaseOrderItems).ToList();
                double accuracyRate = 0;
                if (allItems.Any())
                {
                    double totalOrdered = allItems.Sum(i => (double)i.PoItemQuantity);
                    double totalReceived = allItems.Sum(i => (double)i.ReceivedQuantity);
                    accuracyRate = totalOrdered > 0 ? Math.Round((totalReceived / totalOrdered) * 100, 1) : 0;
                }

                var inspectedPOs = filteredPOs.Where(po => po.QaInspectedDate.HasValue).ToList();
                double avgLeadDays = 0;
                if (inspectedPOs.Any())
                {
                    avgLeadDays = Math.Round(inspectedPOs.Average(po => (po.QaInspectedDate!.Value - po.OrderDate).TotalDays), 1);
                }

                int rejectedCount = inspectedPOs.Count(po => string.Equals(po.QaStatus, "Rejected", StringComparison.OrdinalIgnoreCase));
                double rejectionRate = inspectedPOs.Any() ? Math.Round((rejectedCount / (double)inspectedPOs.Count) * 100, 1) : 0;

                double onTimeRate = totalOrders > 0 ? Math.Round((onTime / (double)totalOrders) * 100, 1) : 0;
                string grade = onTimeRate >= 90 ? "A" : onTimeRate >= 80 ? "B" : onTimeRate >= 70 ? "C" : "F";

                var orderTransactions = filteredPOs
                    .OrderByDescending(po => po.OrderDate)
                    .Select(po => new SupplierOrderTransactionDto
                    {
                        PoId = po.PoId,
                        PoCode = $"PO-{po.PoId:D4}",
                        OrderDate = po.OrderDate.ToString("yyyy-MM-dd"),
                        ExpectedArrivalDate = po.ExpectedArrivalDate.ToString("yyyy-MM-dd"),
                        Status = po.Status == PurchaseOrderStatus.Unspecified
                            ? EnumDbValue.ToDbValue(PurchaseOrderStatus.Pending)
                            : EnumDbValue.ToDbValue(po.Status),
                        TotalItemsCount = po.PurchaseOrderItems?.Count ?? 0,
                        TotalAmount = po.TotalAmount,
                        QaStatus = string.IsNullOrWhiteSpace(po.QaStatus) ? "Pending QA" : po.QaStatus,
                        InspectedDate = po.QaInspectedDate.HasValue ? po.QaInspectedDate.Value.ToString("yyyy-MM-dd HH:mm") : "N/A"
                    })
                    .ToList();

                scorecard.Add(new VendorScorecardAuditDto
                {
                    SupplierId = supplier.SupplierId,
                    SupplierName = supplier.CompanyName,
                    TotalOrdersPlaced = $"{totalOrders} Orders",
                    OnTimeDeliveries = $"{onTime} Orders",
                    LateDeliveries = $"{late} Orders",
                    OrderAccuracyRate = $"{accuracyRate:F1}%",
                    AverageLeadTime = $"{avgLeadDays:F1} Days",
                    RejectionRate = $"{rejectionRate:F1}%",
                    OverallVendorGrade = $"Grade {grade} ({onTimeRate:F1}%)",
                    Orders = orderTransactions
                });
            }

            var response = new SupplierPerformanceReportResponseDto
            {
                VendorScorecard = scorecard.OrderByDescending(s => s.TotalOrdersPlaced)
            };

            return ApiResponse<SupplierPerformanceReportResponseDto>.SuccessResponse(response, "Supplier performance report generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating supplier performance report.");
            return ApiResponse<SupplierPerformanceReportResponseDto>.FailureResponse("An error occurred while generating the supplier report.");
        }
    }

    public async Task<ApiResponse<DistributionReportResponseDto>> GetDistributionReportAsync(ReportFilterDto filter)
    {
        try
        {
            var query = _context.StockTransfers
                .AsNoTracking()
                .Include(t => t.SourceLocation)
                .Include(t => t.DestLocation)
                .Include(t => t.Product)
                    .ThenInclude(p => p.Item)
                .AsQueryable();

            if (filter != null)
            {
                if (filter.StartDate.HasValue)
                    query = query.Where(t => t.TransferDate >= filter.StartDate.Value.ToUniversalTime());
                if (filter.EndDate.HasValue)
                    query = query.Where(t => t.TransferDate <= filter.EndDate.Value.ToUniversalTime());
                if (filter.Month.HasValue)
                    query = query.Where(t => t.TransferDate.Month == filter.Month.Value);
                if (filter.Year.HasValue)
                    query = query.Where(t => t.TransferDate.Year == filter.Year.Value);
            }

            var transfers = await query.OrderByDescending(t => t.TransferDate).ToListAsync();

            var logisticsVelocity = transfers.Select(t => new LogisticsTransferVelocityDto
            {
                TransferId = $"TR-{t.TransferId:D4}",
                SourceLocation = t.SourceLocation?.LocationName ?? "Main Warehouse",
                DestinationBranch = t.DestLocation?.LocationName ?? "Branch Location",
                DispatchDate = t.TransferDate.ToString("yyyy-MM-dd HH:mm"),
                ReceiveDate = t.TransferDate.AddHours(1).ToString("yyyy-MM-dd HH:mm"),
                TransitDuration = "1.0 Hours",
                AssignedDriver = "Assigned Driver",
                TransferStatus = t.Status == ShipmentStatus.Unspecified
                    ? EnumDbValue.ToDbValue(ShipmentStatus.Completed)
                    : EnumDbValue.ToDbValue(t.Status)
            }).ToList();

            var response = new DistributionReportResponseDto
            {
                LogisticsVelocity = logisticsVelocity
            };

            return ApiResponse<DistributionReportResponseDto>.SuccessResponse(response, "Distribution report generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating distribution report.");
            return ApiResponse<DistributionReportResponseDto>.FailureResponse("An error occurred while generating the distribution report.");
        }
    }

    public async Task<ApiResponse<SupplyListReportResponseDto>> GetSupplyListReportAsync(ReportFilterDto filter)
    {
        try
        {
            var items = await _context.Items
                .AsNoTracking()
                .Include(i => i.Category)
                .Include(i => i.Uom)
                .Include(i => i.Inventories)
                .Include(i => i.PurchaseOrderItems)
                    .ThenInclude(poi => poi.Supplier)
                .ToListAsync();

            SupplyListItemDto MapItem(Item i)
            {
                decimal currentStock = i.Inventories?.Sum(inv => inv.CurrentStock) ?? 0m;
                string supplierName = i.PurchaseOrderItems?
                    .Where(poi => poi.Supplier != null)
                    .Select(poi => poi.Supplier!.CompanyName)
                    .FirstOrDefault() ?? "N/A";

                string stockStatus = "Optimal Level";
                decimal suggestedReorder = 0m;

                if (currentStock == 0)
                {
                    stockStatus = "Out of Stock";
                    suggestedReorder = i.MaxStockLevel;
                }
                else if (currentStock < i.MinStockLevel)
                {
                    stockStatus = "Below Min Stock";
                    suggestedReorder = i.MaxStockLevel - currentStock;
                }
                else if (currentStock > i.MaxStockLevel)
                {
                    stockStatus = "Overstocked";
                    suggestedReorder = 0;
                }

                return new SupplyListItemDto
                {
                    ItemNo = i.ItemId,
                    ItemName = i.ItemName,
                    Category = i.Category?.CategoryName ?? "Raw Materials",
                    Unit = i.Uom?.Name ?? i.Uom?.Abbreviation ?? "",
                    PrimarySupplier = supplierName,
                    CurrentStock = currentStock,
                    MinStock = i.MinStockLevel,
                    MaxStock = i.MaxStockLevel,
                    StockStatus = stockStatus,
                    SuggestedReorderQty = Math.Max(0, suggestedReorder),
                    Status = i.IsActive ? "Active" : "Inactive"
                };
            }

            var mappedItems = items.Select(MapItem).ToList();

            var rawMaterials = mappedItems
                .Where(i => !string.Equals(i.Category, "Tools & Supplies", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var tools = mappedItems
                .Where(i => string.Equals(i.Category, "Tools & Supplies", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var summary = new SupplyListSummaryDto
            {
                TotalItemsTracked = mappedItems.Count,
                CriticalLowStockCount = mappedItems.Count(i => i.StockStatus == "Out of Stock" || i.StockStatus == "Below Min Stock"),
                OptimalStockCount = mappedItems.Count(i => i.StockStatus == "Optimal Level"),
                TotalReorderQuantityNeeded = mappedItems.Sum(i => i.SuggestedReorderQty)
            };

            var response = new SupplyListReportResponseDto
            {
                Summary = summary,
                RawMaterials = rawMaterials,
                ToolsAndSupplies = tools
            };

            return ApiResponse<SupplyListReportResponseDto>.SuccessResponse(response, "Supply list inventory health report generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating supply list report.");
            return ApiResponse<SupplyListReportResponseDto>.FailureResponse("An error occurred while generating the supply list report.");
        }
    }

    public async Task<ApiResponse<RecipeReportResponseDto>> GetRecipeReportAsync(ReportFilterDto filter)
    {
        try
        {
            var recipes = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.Product)
                    .ThenInclude(p => p!.Item)
                .Include(r => r.RecipeIngredients)
                .ToListAsync();

            var result = recipes.Select(r => new RecipeReportItemDto
            {
                RecipeNo = r.RecipeId,
                RecipeName = r.RecipeName,
                FinishedProduct = r.Product?.Item?.ItemName ?? r.Product?.Variant ?? "N/A",
                TargetYield = r.OutputQuantity,
                IngredientsCount = r.RecipeIngredients.Count,
                Status = r.IsActive ? "Active" : "Inactive"
            }).ToList();

            var response = new RecipeReportResponseDto
            {
                Recipes = result
            };

            return ApiResponse<RecipeReportResponseDto>.SuccessResponse(response, "Recipe report generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating recipe report.");
            return ApiResponse<RecipeReportResponseDto>.FailureResponse("An error occurred while generating the recipe report.");
        }
    }
}
