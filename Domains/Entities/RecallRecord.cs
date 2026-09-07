using System;

namespace Domains.Entities;

public class RecallRecord
{
    public int RecallId { get; set; }

    /// <summary>Canonical human-readable recall simulation/execution number, e.g. "REC-2026-0001".</summary>
    public string RecallNumber { get; set; } = string.Empty;

    public string TargetLotCode { get; set; } = string.Empty;

    public string Scope { get; set; } = "RawMaterial"; // RawMaterial, Packaging, FinishedGood

    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = "Simulated"; // Simulated, ActiveHold, Resolved

    public DateTime InitiatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public string InitiatedBy { get; set; } = string.Empty;

    public decimal TotalUnitsAffected { get; set; }
    public int BranchesAffectedCount { get; set; }

    public string? AffectedSummaryJson { get; set; }
    public string? Notes { get; set; }
}
