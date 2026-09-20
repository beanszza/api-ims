namespace Domains.Entities;

public class CycleCountItem
{
    public int CycleCountItemId { get; set; }

    public int CycleCountId { get; set; }
    public CycleCount CycleCount { get; set; } = null!;

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public int? LotId { get; set; }
    public InventoryLot? Lot { get; set; }

    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal VarianceQuantity => CountedQuantity - SystemQuantity;

    public decimal UnitCost { get; set; }
    public decimal VarianceCost => VarianceQuantity * UnitCost;

    public string? Reason { get; set; } // Damaged, Misplaced, Spoilage, Shrinkage, Found Stock
    public string? Notes { get; set; }
}
