namespace Domains.Entities;

public class BranchReturnItem
{
    public int BranchReturnItemId { get; set; }

    public int BranchReturnId { get; set; }
    public BranchReturn BranchReturn { get; set; } = null!;

    public int ProductId { get; set; }
    public FinishedProduct Product { get; set; } = null!;

    public int? LotId { get; set; }
    public InventoryLot? Lot { get; set; }

    public decimal ReturnedQuantity { get; set; }

    public string? DefectCondition { get; set; }
    public string? Notes { get; set; }
}
