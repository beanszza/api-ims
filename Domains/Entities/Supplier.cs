namespace Domains.Entities;

public class Supplier
{
    public int SupplierId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    /// <summary>Line items that name this supplier (may duplicate PO header supplier; enforce in application if needed).</summary>
    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
    /// <summary>Batch consumption rows that source material from this supplier.</summary>
    public ICollection<BatchConsumption> SourcedBatchConsumptions { get; set; } = new List<BatchConsumption>();
}