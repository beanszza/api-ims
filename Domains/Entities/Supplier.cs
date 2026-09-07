namespace Domains.Entities;

public class Supplier
{
    public int SupplierId { get; set; }
    
    /// <summary>Unique human-readable identifier (e.g. SUP-2026-0001)</summary>
    public string SupplierCode { get; set; } = string.Empty;
    
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Website { get; set; }
    public bool IsActive { get; set; }

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    /// <summary>Line items that name this supplier (may duplicate PO header supplier; enforce in application if needed).</summary>
    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
    /// <summary>Batch consumption rows that source material from this supplier.</summary>
    public ICollection<BatchConsumption> SourcedBatchConsumptions { get; set; } = new List<BatchConsumption>();
    /// <summary>Catalog of items supplied by this vendor.</summary>
    public ICollection<SupplierItem> SupplierItems { get; set; } = new List<SupplierItem>();
    /// <summary>Regulatory and compliance documents (FDA LTO, Sanitary Permits, COAs).</summary>
    public ICollection<SupplierDocument> SupplierDocuments { get; set; } = new List<SupplierDocument>();
}