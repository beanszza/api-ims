namespace Domains.Entities;

public class FinishedProduct
{
    public int ProductId { get; set; }
    public int ItemId { get; set; }
    public decimal SellingPrice { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;

    // Navigation Properties
    public Item? Item { get; set; }
    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
    public ICollection<ProductionBatch> ProductionBatches { get; set; } = new List<ProductionBatch>();
    public ICollection<StockTransfer> StockTransfers { get; set; } = new List<StockTransfer>();
}