using System.Collections.Generic;

namespace api_scm.Api.Contracts.Responses;

public class InventoryReportDto
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int CurrentStockQuantity { get; set; }
    public int MinimumStockLevel { get; set; }
    public string StockStatus { get; set; } = string.Empty;
    public List<AuditLogDto> RecentAuditLogs { get; set; } = new();
}

public class AuditLogDto
{
    public string FieldName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public System.DateTime Timestamp { get; set; }
    public int UserId { get; set; }
}
