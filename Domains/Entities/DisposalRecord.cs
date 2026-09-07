using System;
using System.Collections.Generic;

namespace Domains.Entities;

public class DisposalRecord
{
    public int DisposalId { get; set; }

    /// <summary>Canonical human-readable disposal document number, e.g. "DIS-2026-0001".</summary>
    public string DisposalNumber { get; set; } = string.Empty;

    public DateTime DisposalDate { get; set; } = DateTime.UtcNow;

    public string Reason { get; set; } = string.Empty; // Expired, Spoilage, Damaged, Quality Recall

    public decimal TotalCost { get; set; }

    public string AuthorizedBy { get; set; } = string.Empty;

    public string? WitnessName { get; set; }

    public string? Notes { get; set; }

    public ICollection<DisposalRecordItem> Items { get; set; } = new List<DisposalRecordItem>();
}
