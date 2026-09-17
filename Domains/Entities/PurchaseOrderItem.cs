using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities;

public class PurchaseOrderItem
{
    public int PoItemId { get; set; }
    public int PoId { get; set; }
    public int ItemId { get; set; }
    public int SupplierId { get; set; }

    /// <summary>Quantity ordered (whole number).</summary>
    public decimal PoItemQuantity { get; set; }

    /// <summary>
    /// Total price for this line as entered by the user.
    /// This is the full amount for all units (not per-unit).
    /// </summary>
    public decimal TotalPrice { get; set; }

    /// <summary>Derived unit price = TotalPrice / PoItemQuantity. Not stored in DB.</summary>
    [NotMapped]
    public decimal UnitPrice => PoItemQuantity > 0 ? TotalPrice / PoItemQuantity : 0;

    /// <summary>Purchasing Unit of Measure (defaults to Item.StockUomId).</summary>
    public int PurchaseUomId { get; set; }

    /// <summary>Running total actually received across all deliveries against this line.</summary>
    public decimal ReceivedQuantity { get; set; }

    [NotMapped]
    public decimal LineTotal => TotalPrice;

    public PurchaseOrder? PurchaseOrder { get; set; }
    public Item? Item { get; set; }
    public Supplier? Supplier { get; set; }
    public UnitOfMeasure? PurchaseUom { get; set; }
}