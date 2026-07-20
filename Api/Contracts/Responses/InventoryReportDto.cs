using System.Collections.Generic;

namespace api_scm.Api.Contracts.Responses;

public class InventoryReportResponseDto
{
    public IEnumerable<HistoricalInventoryAuditDto> HistoricalAudit { get; set; } = new List<HistoricalInventoryAuditDto>();
    public IEnumerable<InventoryDemandForecastDto> DemandForecast { get; set; } = new List<InventoryDemandForecastDto>();
}

public class HistoricalInventoryAuditDto
{
    public string Period { get; set; } = string.Empty;
    public string TotalActiveItems { get; set; } = string.Empty;
    public string StartingStockQty { get; set; } = string.Empty;
    public string EndingStockQty { get; set; } = string.Empty;
    public string StockInQty { get; set; } = string.Empty;
    public string StockOutQty { get; set; } = string.Empty;
    public string WastageQty { get; set; } = string.Empty;
    public string InventoryVelocity { get; set; } = string.Empty;
}

public class InventoryDemandForecastDto
{
    public string ItemName { get; set; } = string.Empty;
    public string CurrentStock { get; set; } = string.Empty;
    public string AvgDailyUsage { get; set; } = string.Empty;
    public string DaysLeft { get; set; } = string.Empty;
    public string RunoutDate { get; set; } = string.Empty;
    public string UrgencyBadge { get; set; } = string.Empty;
    public string RecommendedReorderQty { get; set; } = string.Empty;
}
