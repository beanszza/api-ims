using System;

namespace Domains.Entities;

public class QualityInspectionItem
{
    public int InspectionItemId { get; set; }

    public int InspectionId { get; set; }
    public QualityInspection QualityInspection { get; set; } = null!;

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public int? LotId { get; set; }
    public InventoryLot? Lot { get; set; }

    public decimal DeliveredQuantity { get; set; }

    public decimal AcceptedQuantity { get; set; }

    public decimal RejectedQuantity { get; set; }

    public decimal ConcessionQuantity { get; set; }

    public string? DefectReason { get; set; }

    public string? Notes { get; set; }
}
