namespace Domains.Entities;

public class BatchConsumption
{
    public int BatchConsumptionId { get; set; }
    public int BatchId { get; set; }
    public int ItemId { get; set; }
    public decimal RequiredQuantity { get; set; }
    public int SourceSupplierId { get; set; }

    public ProductionBatch? Batch { get; set; }
    public Item? Item { get; set; }
    public Supplier? SourceSupplier { get; set; }
}