using System;
using System.Collections.Generic;
using Domains.Enums;

namespace Domains.Entities;

public class QualityInspection
{
    public int InspectionId { get; set; }

    /// <summary>Canonical human-readable inspection number, e.g. "QC-IN-2026-0001".</summary>
    public string InspectionNumber { get; set; } = string.Empty;

    public InspectionType InspectionType { get; set; } = InspectionType.Incoming;

    /// <summary>Target entity type: "GRN", "ProductionBatch".</summary>
    public string ReferenceType { get; set; } = string.Empty;

    public int ReferenceId { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public string InspectorId { get; set; } = string.Empty;

    public string InspectorName { get; set; } = string.Empty;

    public DateTime InspectionDate { get; set; } = DateTime.UtcNow;

    public QualityInspectionStatus Status { get; set; } = QualityInspectionStatus.Pending;

    public decimal TotalReceivedQuantity { get; set; }

    public decimal TotalAcceptedQuantity { get; set; }

    public decimal TotalRejectedQuantity { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? CompletedBy { get; set; }

    public string? OverallNotes { get; set; }

    public ICollection<QualityInspectionItem> Items { get; set; } = new List<QualityInspectionItem>();
}
