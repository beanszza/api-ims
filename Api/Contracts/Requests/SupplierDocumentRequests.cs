using System;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreateSupplierDocumentRequest
{
    [Required]
    public int SupplierId { get; set; }

    [Required]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    public string DocumentNumber { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    public DateOnly IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public string? FileUrl { get; set; }
    public string? Notes { get; set; }
}

public class VerifySupplierDocumentRequest
{
    [Required]
    public int DocumentId { get; set; }

    public bool IsVerified { get; set; } = true;
    public string? Notes { get; set; }
}
