namespace Domains.Entities;

public class DisposalRecordItem
{
    public int DisposalItemId { get; set; }

    public int DisposalId { get; set; }
    public DisposalRecord DisposalRecord { get; set; } = null!;

    public int LotId { get; set; }
    public InventoryLot Lot { get; set; } = null!;

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public decimal QuantityDisposed { get; set; }

    public decimal UnitCost { get; set; }

    public string? Notes { get; set; }
}
