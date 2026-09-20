using System;

namespace Domains.Entities;

public class DeliveryItem
{
    public int DeliveryItemId { get; set; }

    public int DeliveryId { get; set; }
    public Delivery Delivery { get; set; } = null!;

    public int PoItemId { get; set; }
    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    /// <summary>Quantity declared by supplier / scheduled for this shipment.</summary>
    public decimal DeclaredQuantity { get; set; }

    public int PurchaseUomId { get; set; }
    public UnitOfMeasure PurchaseUom { get; set; } = null!;
}
