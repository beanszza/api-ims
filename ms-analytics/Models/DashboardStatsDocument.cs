using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ms_analytics.Models;

public class DashboardStatsDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = "dashboard_main";

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastCompiledAt { get; set; } = DateTime.UtcNow;

    public DashboardKpiDto Kpis { get; set; } = new();
    public List<CategoryStockDto> InventoryChart { get; set; } = new();
    public List<MonthlyProcurementDto> ProcurementChart { get; set; } = new();
    public ProductionChartDto ProductionChart { get; set; } = new();
    public List<SupplierPerformanceChartDto> SupplierChart { get; set; } = new();
    public List<BranchTransferChartDto> DistributionChart { get; set; } = new();

    public List<LowStockAlertDto> LowStockAlerts { get; set; } = new();
    public List<ProductProductionFrequencyDto> FrequentlyProducedProducts { get; set; } = new();
    public List<BranchDeliveryVolumeDto> BranchDeliveries { get; set; } = new();
}

public class DashboardKpiDto
{
    public int TotalActiveItems { get; set; }
    public int LowStockCount { get; set; }
    public int TotalBatchesThisMonth { get; set; }
    public double OverallProductionYieldPercent { get; set; }
    public int ActiveSupplierCount { get; set; }
    public int PendingPurchaseOrders { get; set; }
    public int TotalStockTransfersThisMonth { get; set; }
}

public class CategoryStockDto
{
    public string Category { get; set; } = string.Empty;
    public int ItemCount { get; set; }
}

public class MonthlyProcurementDto
{
    public string Month { get; set; } = string.Empty;
    public int TotalOrdersCount { get; set; }
    public double TotalQtyOrdered { get; set; }
}

public class ProductionChartDto
{
    public int TotalBatches { get; set; }
    public int SuccessfulBatches { get; set; }
    public int FailedBatches { get; set; }
    public double YieldSuccessRate { get; set; }
}

public class SupplierPerformanceChartDto
{
    public string SupplierName { get; set; } = string.Empty;
    public double AverageLeadTimeDays { get; set; }
    public int TotalOrdersPlaced { get; set; }
}

public class BranchTransferChartDto
{
    public string BranchName { get; set; } = string.Empty;
    public int TotalTransfers { get; set; }
    public double AverageTransitHours { get; set; }
}

public class LowStockAlertDto
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinStockLevel { get; set; }
    public string Urgency { get; set; } = "WARNING";
}

public class ProductProductionFrequencyDto
{
    public string RecipeName { get; set; } = string.Empty;
    public int BatchCount { get; set; }
    public int TotalOutputQty { get; set; }
}

public class BranchDeliveryVolumeDto
{
    public string BranchName { get; set; } = string.Empty;
    public int DeliveryCount { get; set; }
    public int TotalItemsTransferred { get; set; }
}
