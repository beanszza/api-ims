using System;
using Domains.Enums;

namespace Domains.Entities;

public class PutAwayTransaction
{
    public int PutAwayId { get; set; }

    /// <summary>Canonical human-readable number, e.g. "PA-2026-0001".</summary>
    public string PutAwayNumber { get; set; } = string.Empty;

    public int GrnId { get; set; }
    public GoodsReceipt GoodsReceipt { get; set; } = null!;

    public int GrnItemId { get; set; }
    public GoodsReceiptItem GoodsReceiptItem { get; set; } = null!;

    public int? QaInspectionId { get; set; }
    public QualityInspection? QaInspection { get; set; }

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public decimal AcceptedQuantity { get; set; }

    public int UomId { get; set; }
    public UnitOfMeasure Uom { get; set; } = null!;

    public int? DestinationLocationId { get; set; }
    public Location? DestinationLocation { get; set; }

    public int? LotId { get; set; }
    public InventoryLot? Lot { get; set; }

    public string? LotCode { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? SerialNumber { get; set; }

    public PutAwayStatus Status { get; set; } = PutAwayStatus.Pending;

    public string? PerformedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
}
