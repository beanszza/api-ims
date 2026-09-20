using System;

namespace api_scm.Contracts.Responses;

public class NcrResponse
{
    public int NcrId { get; set; }
    public string NcrNumber { get; set; } = string.Empty;
    public int? InspectionId { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int? LotId { get; set; }
    public string? LotCode { get; set; }
    public decimal DefectiveQuantity { get; set; }
    public string DefectType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class RtvResponse
{
    public int RtvId { get; set; }
    public string RtvNumber { get; set; } = string.Empty;
    public int? NcrId { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public decimal ReturnedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? DispatchedDate { get; set; }
    public string? CreditNoteNumber { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
