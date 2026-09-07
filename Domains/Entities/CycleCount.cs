using System;
using System.Collections.Generic;
using Domains.Enums;

namespace Domains.Entities;

public class CycleCount
{
    public int CycleCountId { get; set; }

    /// <summary>Canonical human-readable cycle count document number, e.g. "CC-2026-0001".</summary>
    public string CountNumber { get; set; } = string.Empty;

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public DateTime CountDate { get; set; } = DateTime.UtcNow;
    public DateTime? ReconciledDate { get; set; }

    public CycleCountStatus Status { get; set; } = CycleCountStatus.Draft;

    public string CountedBy { get; set; } = string.Empty;
    public string? ReconciledBy { get; set; }
    public string? Notes { get; set; }

    public decimal TotalSystemValue { get; set; }
    public decimal TotalCountedValue { get; set; }
    public decimal TotalVarianceValue { get; set; }

    public ICollection<CycleCountItem> Items { get; set; } = new List<CycleCountItem>();
}
