using System;
using Domains.Enums;

namespace Domains.Entities;

public class NonConformanceReport
{
    public int NcrId { get; set; }

    /// <summary>Canonical human-readable NCR number, e.g. "NCR-2026-0001".</summary>
    public string NcrNumber { get; set; } = string.Empty;

    public int? InspectionId { get; set; }
    public QualityInspection? Inspection { get; set; }

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public int? LotId { get; set; }
    public InventoryLot? Lot { get; set; }

    public decimal DefectiveQuantity { get; set; }

    public string DefectType { get; set; } = string.Empty;

    public string Severity { get; set; } = "Major"; // Minor, Major, Critical

    public string? RootCause { get; set; }

    public string? CorrectiveAction { get; set; }

    public NcrStatus Status { get; set; } = NcrStatus.Open;

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
