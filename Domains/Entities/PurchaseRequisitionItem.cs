using System;

namespace Domains.Entities;

public class PurchaseRequisitionItem
{
    public int PrItemId { get; set; }

    public int PrId { get; set; }
    public PurchaseRequisition PurchaseRequisition { get; set; } = null!;

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public int? SuggestedSupplierId { get; set; }
    public Supplier? SuggestedSupplier { get; set; }

    public decimal RequestedQuantity { get; set; }

    public int PurchaseUomId { get; set; }
    public UnitOfMeasure PurchaseUom { get; set; } = null!;

    public decimal EstimatedUnitPrice { get; set; }

    public decimal EstimatedLineTotal => RequestedQuantity * EstimatedUnitPrice;
}
