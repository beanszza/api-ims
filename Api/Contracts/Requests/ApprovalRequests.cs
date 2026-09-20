using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreateApprovalRequest
{
    [Required]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public int EntityId { get; set; }

    [Required]
    public string DocumentNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    [Required]
    public string Reason { get; set; } = string.Empty;
}

public class ActOnApprovalRequest
{
    [Required]
    public bool Approve { get; set; }

    public string? Comments { get; set; }
}
