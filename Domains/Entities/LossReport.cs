using System;

namespace Domains.Entities;

public class LossReport
{
    public int LossReportId { get; set; }

    /// <summary>Canonical human-readable number, e.g. "LR-2026-0001".</summary>
    public string LossReportNumber { get; set; } = string.Empty;

    public int? DiscrepancyId { get; set; }
    public Discrepancy? Discrepancy { get; set; }

    public int? GrnId { get; set; }
    public GoodsReceipt? GoodsReceipt { get; set; }

    public int? PoId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public int? LotId { get; set; }
    public InventoryLot? Lot { get; set; }

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public decimal LostQuantity { get; set; }

    public int UomId { get; set; }
    public UnitOfMeasure Uom { get; set; } = null!;

    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public bool IsAcknowledged { get; set; } = false;
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }

    public string AuthorisedBy { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public long? StockLedgerEntryId { get; set; }
    public StockLedger? StockLedgerEntry { get; set; }
}
