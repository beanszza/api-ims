namespace api_scm.Contracts.Responses;

public class InventoryResponse
{
    public int InventoryId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UomName { get; set; } = string.Empty;
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinStockLevel { get; set; }
    public bool IsLowStock => CurrentStock <= MinStockLevel;
}
