using System;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreateNcrRequest
{
    public int? InspectionId { get; set; }

    [Required]
    public int SupplierId { get; set; }

    [Required]
    public int ItemId { get; set; }

    public int? LotId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Defective quantity must be positive.")]
    public decimal DefectiveQuantity { get; set; }

    [Required]
    public string DefectType { get; set; } = string.Empty;

    public string Severity { get; set; } = "Major"; // Minor, Major, Critical

    public string? RootCause { get; set; }

    public string? CorrectiveAction { get; set; }
}

public class ResolveNcrRequest
{
    [Required]
    public string Status { get; set; } = "Resolved"; // Resolved, Closed

    public string? CorrectiveAction { get; set; }
}

public class CreateRtvRequest
{
    public int? NcrId { get; set; }

    [Required]
    public int SupplierId { get; set; }

    [Required]
    public int ItemId { get; set; }

    [Required]
    public int LotId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Returned quantity must be positive.")]
    public decimal ReturnedQuantity { get; set; }

    [Required]
    public string Reason { get; set; } = string.Empty;
}

public class DispatchRtvRequest
{
    public string? CreditNoteNumber { get; set; }
}
