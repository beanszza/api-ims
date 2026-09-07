using System;
using System.Collections.Generic;
using Domains.Enums;

namespace Domains.Entities;

public class BranchRequest
{
    public int BranchRequestId { get; set; }

    /// <summary>Canonical human-readable branch request number, e.g. "BR-2026-0001".</summary>
    public string RequestNumber { get; set; } = string.Empty;

    public int BranchId { get; set; }
    public Location Branch { get; set; } = null!;

    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public DateTime? RequiredDate { get; set; }

    public BranchRequestStatus Status { get; set; } = BranchRequestStatus.Pending;

    public string RequestedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string? Notes { get; set; }

    public ICollection<BranchRequestItem> Items { get; set; } = new List<BranchRequestItem>();
}
