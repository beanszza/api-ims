using System;
using Domains.Enums;

namespace Domains.Entities;

public class ReturnToVendor
{
    public int RtvId { get; set; }

    /// <summary>Canonical human-readable RTV number, e.g. "RTV-2026-0001".</summary>
    public string RtvNumber { get; set; } = string.Empty;

    public int? NcrId { get; set; }
    public NonConformanceReport? NonConformanceReport { get; set; }

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public int LotId { get; set; }
    public InventoryLot Lot { get; set; } = null!;

    public decimal ReturnedQuantity { get; set; }

    public string Reason { get; set; } = string.Empty;

    public RtvStatus Status { get; set; } = RtvStatus.PendingDispatch;

    public DateTime? DispatchedDate { get; set; }

    public string? CreditNoteNumber { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
