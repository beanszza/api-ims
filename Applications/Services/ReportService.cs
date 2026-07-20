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

    public async Task<ApiResponse<InventoryReportResponseDto>> GetInventoryReportAsync(ReportFilterDto filter)
    {
        var response = new InventoryReportResponseDto
        {
            HistoricalAudit = new List<HistoricalInventoryAuditDto>
            {
                new HistoricalInventoryAuditDto { Period = "June 2026", TotalActiveItems = "48 Items", StartingStockQty = "1,450 kg", EndingStockQty = "1,120 kg", StockInQty = "800 kg", StockOutQty = "1,080 kg", WastageQty = "50 kg", InventoryVelocity = "83.7%" },
                new HistoricalInventoryAuditDto { Period = "May 2026", TotalActiveItems = "46 Items", StartingStockQty = "1,320 kg", EndingStockQty = "1,450 kg", StockInQty = "950 kg", StockOutQty = "790 kg", WastageQty = "30 kg", InventoryVelocity = "72.4%" },
                new HistoricalInventoryAuditDto { Period = "April 2026", TotalActiveItems = "45 Items", StartingStockQty = "1,100 kg", EndingStockQty = "1,320 kg", StockInQty = "1,100 kg", StockOutQty = "840 kg", WastageQty = "40 kg", InventoryVelocity = "75.1%" }
            },
            DemandForecast = new List<InventoryDemandForecastDto>
            {
                new InventoryDemandForecastDto { ItemName = "White Sugar", CurrentStock = "12 kg", AvgDailyUsage = "3.5 kg/day", DaysLeft = "3 Days", RunoutDate = "July 23, 2026", UrgencyBadge = "CRITICAL", RecommendedReorderQty = "88 kg" },
                new InventoryDemandForecastDto { ItemName = "All-Purpose Flour", CurrentStock = "18 kg", AvgDailyUsage = "2.0 kg/day", DaysLeft = "9 Days", RunoutDate = "July 29, 2026", UrgencyBadge = "WARNING", RecommendedReorderQty = "82 kg" },
                new InventoryDemandForecastDto { ItemName = "Butter", CurrentStock = "45 kg", AvgDailyUsage = "2.1 kg/day", DaysLeft = "21 Days", RunoutDate = "August 10, 2026", UrgencyBadge = "NORMAL", RecommendedReorderQty = "0 kg" }
            }
        };
        return ApiResponse<InventoryReportResponseDto>.SuccessResponse(response, "Inventory report generated successfully");
    }

    public async Task<ApiResponse<ProcurementReportResponseDto>> GetProcurementReportAsync(ReportFilterDto filter)
    {
        var response = new ProcurementReportResponseDto
        {
            HistoricalAudit = new List<HistoricalProcurementAuditDto>
            {
                new HistoricalProcurementAuditDto { PoId = "PO-2026-089", IssueDate = "2026-06-12", SupplierName = "Supplier A - Batangas Flour", TotalItemsCount = "4 Items", TotalOrderedQty = "1,200 kg", DeliveryLeadTime = "3.2 Days", FulfillmentRate = "100%", InspectionStatus = "Passed" },
                new HistoricalProcurementAuditDto { PoId = "PO-2026-082", IssueDate = "2026-06-04", SupplierName = "Supplier B - Manila Sugar", TotalItemsCount = "2 Items", TotalOrderedQty = "850 kg", DeliveryLeadTime = "6.8 Days", FulfillmentRate = "92.5%", InspectionStatus = "Partial Pass" }
            },
            ProcurementAdvice = new List<SmartProcurementAdviceDto>
            {
                new SmartProcurementAdviceDto { ItemName = "White Sugar", OrderTrend = "Upward +15% / month", PredictedNextMonthQty = "185 kg", AiConfidence = "High", RecommendationBasis = "Consistent upward purchasing trend over 6 months." },
                new SmartProcurementAdviceDto { ItemName = "All-Purpose Flour", OrderTrend = "Stable", PredictedNextMonthQty = "202 kg", AiConfidence = "Medium", RecommendationBasis = "Stable historical demand with minor seasonal fluctuations." }
            }
        };
        return ApiResponse<ProcurementReportResponseDto>.SuccessResponse(response, "Procurement report generated successfully");
    }

    public async Task<ApiResponse<ProductionQualityReportResponseDto>> GetProductionReportAsync(ReportFilterDto filter)
    {
        var response = new ProductionQualityReportResponseDto
        {
            YieldEfficiency = new List<KitchenYieldEfficiencyDto>
            {
                new KitchenYieldEfficiencyDto { RecipeName = "Pan de Sal Batch A", TotalBatchesCooked = "20 Batches", TotalOutputQty = "2,400 Pcs", YieldSuccessRate = "95.0%", TotalRejectedQty = "120 Pcs", IngredientWasteQty = "4.2 kg", CommonFailureReason = "Over-baking / Crust Burn" },
                new KitchenYieldEfficiencyDto { RecipeName = "Spanish Bread Batch B", TotalBatchesCooked = "14 Batches", TotalOutputQty = "1,120 Pcs", YieldSuccessRate = "92.8%", TotalRejectedQty = "80 Pcs", IngredientWasteQty = "3.1 kg", CommonFailureReason = "Dough Proofing Under-expansion" }
            }
        };
        return ApiResponse<ProductionQualityReportResponseDto>.SuccessResponse(response, "Production report generated successfully");
    }

    public async Task<ApiResponse<SupplierPerformanceReportResponseDto>> GetSupplierReportAsync(ReportFilterDto filter)
    {
        var response = new SupplierPerformanceReportResponseDto
        {
            VendorScorecard = new List<VendorScorecardAuditDto>
            {
                new VendorScorecardAuditDto { SupplierName = "Supplier A - Flour Corp", TotalOrdersPlaced = "15 Orders", OnTimeDeliveries = "15 Orders", LateDeliveries = "0 Orders", OrderAccuracyRate = "98.5%", AverageLeadTime = "3.2 Days", RejectionRate = "0.0%", OverallVendorGrade = "Grade A 98.5%" },
                new VendorScorecardAuditDto { SupplierName = "Supplier B - Sugar Traders", TotalOrdersPlaced = "12 Orders", OnTimeDeliveries = "10 Orders", LateDeliveries = "2 Orders", OrderAccuracyRate = "89.0%", AverageLeadTime = "6.8 Days", RejectionRate = "8.3%", OverallVendorGrade = "Grade B 84.2%" }
            }
        };
        return ApiResponse<SupplierPerformanceReportResponseDto>.SuccessResponse(response, "Supplier report generated successfully");
    }

    public async Task<ApiResponse<DistributionReportResponseDto>> GetDistributionReportAsync(ReportFilterDto filter)
    {
        var response = new DistributionReportResponseDto
        {
            LogisticsVelocity = new List<LogisticsTransferVelocityDto>
            {
                new LogisticsTransferVelocityDto { TransferId = "TR-2026-104", SourceLocation = "Central Warehouse", DestinationBranch = "Branch 1 - Quezon City", DispatchDate = "2026-06-15 08:00", ReceiveDate = "2026-06-15 09:30", TransitDuration = "1.5 Hours", AssignedDriver = "Driver Juan Cruz", TransferStatus = "Delivered" },
                new LogisticsTransferVelocityDto { TransferId = "TR-2026-108", SourceLocation = "Central Warehouse", DestinationBranch = "Branch 2 - Makati", DispatchDate = "2026-06-16 10:00", ReceiveDate = "2026-06-16 12:06", TransitDuration = "2.1 Hours", AssignedDriver = "Driver Mario Santos", TransferStatus = "Delivered" }
            }
        };
        return ApiResponse<DistributionReportResponseDto>.SuccessResponse(response, "Distribution report generated successfully");
    }
}
