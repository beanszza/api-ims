using System;

namespace Domains.Entities;

public class GoodsReceiptItem
{
    public int GrnItemId { get; set; }

    public int GrnId { get; set; }
    public GoodsReceipt GoodsReceipt { get; set; } = null!;

    public int PoItemId { get; set; }
    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public decimal OrderedQuantity { get; set; }

    public decimal DeliveredQuantity { get; set; }

    public int PurchaseUomId { get; set; }
    public UnitOfMeasure PurchaseUom { get; set; } = null!;

    public int? LotId { get; set; }
    public InventoryLot? Lot { get; set; }

    public string? SupplierLotCode { get; set; }

    public DateOnly? ManufactureDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }
}
