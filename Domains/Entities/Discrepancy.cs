using System;
using Domains.Enums;

namespace Domains.Entities;

public class Discrepancy
{
    public int DiscrepancyId { get; set; }

    /// <summary>Canonical human-readable number, e.g. "DSC-2026-0001".</summary>
    public string DiscrepancyNumber { get; set; } = string.Empty;

    public DiscrepancyType DiscrepancyType { get; set; }

    public int GrnId { get; set; }
    public GoodsReceipt GoodsReceipt { get; set; } = null!;

    public string GrnNumber { get; set; } = string.Empty;

    public int PoId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public string PoNumber { get; set; } = string.Empty;

    public int? DeliveryId { get; set; }
    public Delivery? Delivery { get; set; }

    public string? DeliveryNumber { get; set; }

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public decimal OrderedQuantity { get; set; }
    public decimal PreviouslyReceivedQty { get; set; }
    public decimal CurrentReceivedQty { get; set; }
    public decimal DiscrepancyQuantity { get; set; }

    public DiscrepancyStatus Status { get; set; } = DiscrepancyStatus.Open;

    public string? ResolutionType { get; set; } // "NewDelivery", "CloseRemaining", "LossReport", "ReturnToSupplier", "KeepWithCredit"
    public string? ResolutionNotes { get; set; }

    public int? LossReportId { get; set; }
    public LossReport? LossReport { get; set; }

    public int? RtvId { get; set; }
    public ReturnToVendor? ReturnToVendor { get; set; }

    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? NcrId { get; set; }
    public NonConformanceReport? NonConformanceReport { get; set; }
}
