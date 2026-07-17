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

namespace Applications.Services;

public class ReportService : IReportService
{
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

    public async Task<ApiResponse<IEnumerable<InventoryReportDto>>> GetInventoryReportAsync(ReportFilterDto filter)
    {
        try
        {
            var inventories = await _context.Inventories
                .Include(i => i.Item)
                .ThenInclude(it => it.Category)
                .ToListAsync();

            // Note: Audit logs can be large, we'll fetch them separately if needed, 
            // but for now we'll retrieve a small set.
            var recentAuditLogs = await _context.AuditLogs
                .Where(a => a.EntityName == "Item" || a.EntityName == "Inventory")
                .OrderByDescending(a => a.Timestamp)
                .Take(100)
                .ToListAsync();

            var groupedByItem = inventories
                .Where(i => i.Item != null)
                .GroupBy(i => i.Item!)
                .Select(g =>
                {
                    var totalStock = g.Sum(i => i.CurrentStock);
                    var minLevel = g.Key.MinStockLevel;
                    var status = totalStock <= 0 ? "Critical" : totalStock <= minLevel ? "Low" : "Normal";

                    var itemAuditLogs = recentAuditLogs
                        .Where(a => (a.EntityName == "Item" && a.EntityId == g.Key.ItemId.ToString()) || 
                                    (a.EntityName == "Inventory" && g.Any(inv => inv.InventoryId.ToString() == a.EntityId)))
                        .Select(a => new AuditLogDto
                        {
                            Action = a.Action,
                            FieldName = a.FieldName,
                            NewValue = a.NewValue,
                            OldValue = a.OldValue,
                            Timestamp = a.Timestamp,
                            UserId = a.UserId
                        })
                        .ToList();

                    return new InventoryReportDto
                    {
                        ItemId = g.Key.ItemId,
                        ItemName = g.Key.ItemName,
                        CategoryName = g.Key.Category?.CategoryName ?? "Unknown",
                        CurrentStockQuantity = totalStock,
                        MinimumStockLevel = minLevel,
                        StockStatus = status,
                        RecentAuditLogs = itemAuditLogs
                    };
                })
                .ToList();

            return ApiResponse<IEnumerable<InventoryReportDto>>.SuccessResponse(groupedByItem, "Inventory report generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Inventory Report");
            return ApiResponse<IEnumerable<InventoryReportDto>>.FailureResponse("An error occurred while retrieving report data.");
        }
    }

    public async Task<ApiResponse<IEnumerable<ProcurementReportDto>>> GetProcurementReportAsync(ReportFilterDto filter)
    {
        try
        {
            var pos = await _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.PurchaseOrderItems)
                .ToListAsync();

            var filteredPos = pos.Where(po => IsDateInRange(po.OrderDate, filter)).ToList();

            var report = filteredPos.Select(po =>
            {
                var totalOrdered = po.PurchaseOrderItems.Sum(poi => poi.PoItemQuantity);
                var totalReceived = po.PurchaseOrderItems.Sum(poi => poi.ReceivedQuantity);
                double fulfillment = totalOrdered > 0 ? (double)totalReceived / totalOrdered * 100 : 0;
                double leadTime = (po.ExpectedArrivalDate - po.OrderDate).TotalDays;

                return new ProcurementReportDto
                {
                    PoId = po.PoId,
                    SupplierName = po.Supplier?.CompanyName ?? "Unknown",
                    Status = po.Status,
                    FulfillmentRate = Math.Round(fulfillment, 2),
                    LeadTimeDays = Math.Round(leadTime > 0 ? leadTime : 0, 2),
                    OrderDate = po.OrderDate
                };
            }).ToList();

            return ApiResponse<IEnumerable<ProcurementReportDto>>.SuccessResponse(report, "Procurement report generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Procurement Report");
            return ApiResponse<IEnumerable<ProcurementReportDto>>.FailureResponse("An error occurred while retrieving report data.");
        }
    }

    public async Task<ApiResponse<IEnumerable<ProductionQualityReportDto>>> GetProductionReportAsync(ReportFilterDto filter)
    {
        try
        {
            var batches = await _context.ProductionBatches
                .Include(b => b.Recipe)
                .ToListAsync();

            var filteredBatches = batches.Where(b => IsDateInRange(b.ProductionDate, filter)).ToList();

            var report = filteredBatches.Select(b =>
            {
                double yieldRate = b.EstimatedQuantity > 0 ? (double)b.ActualQuantity / b.EstimatedQuantity * 100 : 0;
                int rejected = b.EstimatedQuantity > b.ActualQuantity ? b.EstimatedQuantity - b.ActualQuantity : 0;

                return new ProductionQualityReportDto
                {
                    BatchId = b.BatchId,
                    RecipeName = b.Recipe?.RecipeName ?? "Unknown",
                    YieldSuccessRate = Math.Round(yieldRate, 2),
                    IngredientWaste = 0, // Not accurately trackable with current schema
                    TotalRejectedQuantity = rejected,
                    CommonFailureReason = string.IsNullOrWhiteSpace(b.RejectionReason) ? "None" : b.RejectionReason,
                    ProductionDate = b.ProductionDate
                };
            }).ToList();

            return ApiResponse<IEnumerable<ProductionQualityReportDto>>.SuccessResponse(report, "Production report generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Production Report");
            return ApiResponse<IEnumerable<ProductionQualityReportDto>>.FailureResponse("An error occurred while retrieving report data.");
        }
    }

    public async Task<ApiResponse<IEnumerable<SupplierPerformanceReportDto>>> GetSupplierReportAsync(ReportFilterDto filter)
    {
        try
        {
            var suppliers = await _context.Suppliers
                .Include(s => s.PurchaseOrders)
                    .ThenInclude(po => po.PurchaseOrderItems)
                .ToListAsync();

            var currentDate = DateTime.UtcNow;

            var report = suppliers.Select(s =>
            {
                // Optionally apply date filters to the POs of the supplier
                var pos = s.PurchaseOrders.Where(po => IsDateInRange(po.OrderDate, filter)).ToList();

                int completed = pos.Count(po => po.Status.Contains("Completed", StringComparison.OrdinalIgnoreCase) || po.Status.Contains("Received", StringComparison.OrdinalIgnoreCase));
                int overdue = pos.Count(po => po.Status.Contains("Pending", StringComparison.OrdinalIgnoreCase) && currentDate > po.ExpectedArrivalDate);
                
                double accuracy = pos.Any() ? pos.Average(po => 
                {
                    var totalOrdered = po.PurchaseOrderItems.Sum(poi => poi.PoItemQuantity);
                    var totalReceived = po.PurchaseOrderItems.Sum(poi => poi.ReceivedQuantity);
                    return totalOrdered > 0 ? (double)totalReceived / totalOrdered * 100 : 0;
                }) : 0;

                double avgLeadTime = pos.Any() ? pos.Average(po => (po.ExpectedArrivalDate - po.OrderDate).TotalDays) : 0;

                string grade = "C";
                if (accuracy > 90 && overdue == 0) grade = "A";
                else if (accuracy > 70) grade = "B";

                return new SupplierPerformanceReportDto
                {
                    SupplierId = s.SupplierId,
                    SupplierName = s.CompanyName,
                    CompletedOrders = completed,
                    OverdueOrders = overdue,
                    OrderAccuracyRate = Math.Round(accuracy, 2),
                    AverageLeadTimeDays = Math.Round(avgLeadTime > 0 ? avgLeadTime : 0, 2),
                    RejectionRate = 0, // Not available in schema
                    OverallVendorGrade = grade
                };
            }).ToList();

            return ApiResponse<IEnumerable<SupplierPerformanceReportDto>>.SuccessResponse(report, "Supplier report generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Supplier Report");
            return ApiResponse<IEnumerable<SupplierPerformanceReportDto>>.FailureResponse("An error occurred while retrieving report data.");
        }
    }

    public async Task<ApiResponse<IEnumerable<DistributionReportDto>>> GetDistributionReportAsync(ReportFilterDto filter)
    {
        try
        {
            var transfers = await _context.StockTransfers
                .Include(st => st.SourceLocation)
                .Include(st => st.DestLocation)
                .ToListAsync();

            var filteredTransfers = transfers.Where(st => IsDateInRange(st.TransferDate, filter)).ToList();

            if (!filteredTransfers.Any())
            {
                // Returns an empty dataset as per requirement for "No Data Found" state.
                return ApiResponse<IEnumerable<DistributionReportDto>>.SuccessResponse(new List<DistributionReportDto>(), "No data found for distribution report");
            }

            var report = filteredTransfers.Select(st => new DistributionReportDto
            {
                TransferId = st.TransferId,
                SourceLocation = st.SourceLocation?.LocationName ?? "Unknown",
                DestLocation = st.DestLocation?.LocationName ?? "Unknown",
                DriverName = "Not Assigned", // Missing DriverId in StockTransfer
                TransitDurationHours = 0, // Missing ArrivalDate
                Status = st.Status,
                TransferDate = st.TransferDate
            }).ToList();

            return ApiResponse<IEnumerable<DistributionReportDto>>.SuccessResponse(report, "Distribution report generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Distribution Report");
            return ApiResponse<IEnumerable<DistributionReportDto>>.FailureResponse("An error occurred while retrieving report data.");
        }
    }
}
