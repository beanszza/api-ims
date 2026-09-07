using System;

namespace api_scm.Contracts.Responses;

public class SupplierDocumentResponse
{
    public int DocumentId { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? FileUrl { get; set; }
    public bool IsVerified { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? Notes { get; set; }
    public bool IsExpired { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public string Status { get; set; } = string.Empty; // "Valid", "Expiring Soon", "Expired", "Unverified"
}

public class SupplierComplianceSummaryResponse
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public bool HasValidFdaLto { get; set; }
    public bool HasValidSanitaryPermit { get; set; }
    public bool IsFullyCompliant => HasValidFdaLto && HasValidSanitaryPermit;
    public int TotalDocumentsCount { get; set; }
    public int ExpiredDocumentsCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public List<SupplierDocumentResponse> Documents { get; set; } = new();
}
