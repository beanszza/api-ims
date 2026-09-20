using System;
using System.Collections.Generic;
using Domains.Enums;

namespace Domains.Entities;

public class PurchaseRequisition
{
    public int PrId { get; set; }

    /// <summary>Canonical human-readable PR number, e.g. "PR-2026-0001".</summary>
    public string PrNumber { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public string RequestedBy { get; set; } = string.Empty;

    public DateTime RequestDate { get; set; } = DateTime.UtcNow;

    public DateTime RequiredDate { get; set; }

    public PurchaseRequisitionStatus Status { get; set; } = PurchaseRequisitionStatus.Draft;
    
    public string? RequestType { get; set; }

    public string? Priority { get; set; }

    public string? Purpose { get; set; }

    public string? Notes { get; set; }

    public string? AdminNotes { get; set; }

    public decimal EstimatedTotalAmount { get; set; }

    public string? GeneratedPoNumbers { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<PurchaseRequisitionItem> Items { get; set; } = new List<PurchaseRequisitionItem>();
}
