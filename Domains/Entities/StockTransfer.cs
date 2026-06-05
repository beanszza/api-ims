namespace Domains.Entities;

public class StockTransfer
{
    public int TransferId { get; set; }
    public int ProductId { get; set; }
    public int SourceLocationId { get; set; }
    public int DestLocationId { get; set; }
    public int TransferQuantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;

    public FinishedProduct? Product { get; set; }

    // We explicitly name these so EF Core knows which is which
    public Location? SourceLocation { get; set; }
    public Location? DestLocation { get; set; }
}