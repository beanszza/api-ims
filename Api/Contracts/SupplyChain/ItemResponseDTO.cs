namespace api_scm.Contracts.Responses;

public class ItemResponse
{
    public int ItemId { get; set; }
    public int UomId { get; set; }
    public int CategoryId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int MinStockLevel { get; set; }
    public int MaxStockLevel { get; set; }
    public bool IsActive { get; set; }
    public string UomName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public double StockPercentage => MinStockLevel > 0
        ? (double)CurrentStock / MinStockLevel * 100
        : 100;
    public bool IsLowStock => CurrentStock < MinStockLevel;
}