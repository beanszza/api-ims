using System;
using Domains.Enums;

namespace Domains.Entities;

public class ApprovalRequest
{
    public int ApprovalRequestId { get; set; }

    /// <summary>Target entity type (e.g., "PurchaseOrder", "PurchaseRequisition", "DisposalDocument").</summary>
    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    /// <summary>Readable reference document number, e.g. "PO-2026-0042".</summary>
    public string DocumentNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    public string Reason { get; set; } = string.Empty;

    public string RequestedBy { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public string? ApproverId { get; set; }

    public string? ApproverName { get; set; }

    public DateTime? ActionAt { get; set; }

    public string? Comments { get; set; }
}
