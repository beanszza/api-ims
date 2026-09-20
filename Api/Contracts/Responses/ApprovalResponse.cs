using System;

namespace api_scm.Contracts.Responses;

public class ApprovalRequestResponse
{
    public int ApprovalRequestId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? ActionAt { get; set; }
    public string? Comments { get; set; }
}
