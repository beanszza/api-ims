using System;
using System.Collections.Generic;

namespace Domains.Entities;

public class BranchReturn
{
    public int BranchReturnId { get; set; }

    /// <summary>Canonical human-readable branch return number, e.g. "RET-2026-0001".</summary>
    public string ReturnNumber { get; set; } = string.Empty;

    public int BranchId { get; set; }
    public Location Branch { get; set; } = null!;

    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

    public string Reason { get; set; } = string.Empty; // Near Expiry, Damaged in Store, Customer Return

    public string Status { get; set; } = "Pending"; // Pending, ReceivedAtWarehouse, Condemned

    public string ReturnedBy { get; set; } = string.Empty;
    public string? AuthorizedBy { get; set; }
    public string? Notes { get; set; }

    public ICollection<BranchReturnItem> Items { get; set; } = new List<BranchReturnItem>();
}
