using System.Collections.Generic;

namespace api_scm.Api.Contracts.Responses;

public class SupplyListReportResponseDto
{
    public SupplyListSummaryDto Summary { get; set; } = new SupplyListSummaryDto();
    public IEnumerable<SupplyListItemDto> RawMaterials { get; set; } = new List<SupplyListItemDto>();
    public IEnumerable<SupplyListItemDto> ToolsAndSupplies { get; set; } = new List<SupplyListItemDto>();
}

public class SupplyListSummaryDto
{
    public int TotalItemsTracked { get; set; }
    public int CriticalLowStockCount { get; set; }
    public int OptimalStockCount { get; set; }
    public decimal TotalReorderQuantityNeeded { get; set; }
}

public class SupplyListItemDto
{
    public int ItemNo { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string PrimarySupplier { get; set; } = "N/A";
    public decimal CurrentStock { get; set; }
    public decimal MinStock { get; set; }
    public decimal MaxStock { get; set; }
    public string StockStatus { get; set; } = string.Empty;
    public decimal SuggestedReorderQty { get; set; }
    public string Status { get; set; } = string.Empty;
}
