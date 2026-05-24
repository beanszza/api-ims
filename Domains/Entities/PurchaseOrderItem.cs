namespace Domains.Entities;

public class PurchaseOrderItem
{
    public int PoItemId { get; set; }
    public int PoId { get; set; }
    public int ItemId { get; set; }
    public int SupplierId { get; set; }
    public int PoItemQuantity { get; set; }
    public int ReceivedQuantity { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
    public Item? Item { get; set; }
    public Supplier? Supplier { get; set; }
}