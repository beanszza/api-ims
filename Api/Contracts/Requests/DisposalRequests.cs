using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreateDisposalRequest
{
    [Required]
    public string Reason { get; set; } = "Expired"; // Expired, Spoilage, Damaged, Quality Recall

    public string? WitnessName { get; set; }

    public string? Notes { get; set; }

    [Required]
    public List<CreateDisposalItemRequest> Items { get; set; } = new();
}

public class CreateDisposalItemRequest
{
    [Required]
    public int LotId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Quantity disposed must be positive.")]
    public decimal QuantityDisposed { get; set; }

    public string? Notes { get; set; }
}
