using System;
using Domains.Enums;

namespace Domains.Entities;

/// <summary>
/// Regulatory compliance and quality assurance document provided by a supplier.
/// Tracks FDA License to Operate (LTO), Sanitary Permits, and COA certificates with validity dates.
/// </summary>
public class SupplierDocument
{
    public int DocumentId { get; set; }
    public int SupplierId { get; set; }

    public SupplierDocumentType DocumentType { get; set; } = SupplierDocumentType.Unspecified;

    /// <summary>Official permit or license registration number.</summary>
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>Descriptive title or name of the document.</summary>
    public string Title { get; set; } = string.Empty;

    public DateOnly IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>Storage path or document link.</summary>
    public string? FileUrl { get; set; }

    /// <summary>Whether this document has been reviewed and verified by QA/Compliance officer.</summary>
    public bool IsVerified { get; set; }

    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Supplier? Supplier { get; set; }

    // Helper computed properties
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateOnly.FromDateTime(DateTime.Today);
    public int? DaysUntilExpiry => ExpiryDate.HasValue
        ? ExpiryDate.Value.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber
        : null;
}
