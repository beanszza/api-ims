namespace Domains.Entities;

public class PurchaseOrderItem
{
    public int PoItemId { get; set; }
    public int PoId { get; set; }
    public int ItemId { get; set; }
    public int SupplierId { get; set; }

    /// <summary>Quantity ordered.</summary>
    public decimal PoItemQuantity { get; set; }

    /// <summary>Agreed purchase unit price.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Purchasing Unit of Measure (defaults to Item.StockUomId).</summary>
    public int PurchaseUomId { get; set; }

    /// <summary>Running total actually received across all deliveries against this line.</summary>
    public decimal ReceivedQuantity { get; set; }

    public decimal LineTotal => PoItemQuantity * UnitPrice;

    public PurchaseOrder? PurchaseOrder { get; set; }
    public Item? Item { get; set; }
    public Supplier? Supplier { get; set; }
    public UnitOfMeasure? PurchaseUom { get; set; }
}