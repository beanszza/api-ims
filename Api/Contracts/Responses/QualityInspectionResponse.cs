using System;
using System.Collections.Generic;

namespace api_scm.Contracts.Responses;

public class QualityInspectionResponse
{
    public int InspectionId { get; set; }
    public string InspectionNumber { get; set; } = string.Empty;
    public string InspectionType { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = string.Empty;
    public int ReferenceId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public int? PoId { get; set; }
    public string? PoNumber { get; set; }
    public int? PrId { get; set; }
    public string? PrNumber { get; set; }
    public string? SupplierName { get; set; }
    public string InspectorId { get; set; } = string.Empty;
    public string InspectorName { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalReceivedQuantity { get; set; }
    public decimal TotalAcceptedQuantity { get; set; }
    public decimal TotalRejectedQuantity { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public string? OverallNotes { get; set; }
    public List<QualityInspectionItemResponse> Items { get; set; } = new();
}

public class QualityInspectionItemResponse
{
    public int InspectionItemId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int? LotId { get; set; }
    public string? LotCode { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public decimal ConcessionQuantity { get; set; }
    public string? DefectReason { get; set; }
    public string? Notes { get; set; }
}
