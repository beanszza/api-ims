using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreateQualityInspectionRequest
{
    [Required]
    public string InspectionType { get; set; } = "Incoming"; // Incoming, InProcess, FinishedGoods

    [Required]
    public string ReferenceType { get; set; } = "GRN"; // GRN, ProductionBatch

    [Required]
    public int ReferenceId { get; set; }

    public string? OverallNotes { get; set; }

    [Required]
    public List<CreateQualityInspectionItemRequest> Items { get; set; } = new();
}

public class CompleteQualityInspectionRequest
{
    public string? OverallNotes { get; set; }

    [Required]
    public List<CompleteQualityInspectionItemRequest> Items { get; set; } = new();
}

public class CompleteQualityInspectionItemRequest
{
    public int? InspectionItemId { get; set; }

    [Required]
    public int ItemId { get; set; }

    public int? LotId { get; set; }

    public decimal DeliveredQuantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Accepted quantity cannot be negative.")]
    public decimal AcceptedQuantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Rejected quantity cannot be negative.")]
    public decimal RejectedQuantity { get; set; }

    public decimal ConcessionQuantity { get; set; }

    public string? DefectReason { get; set; }

    public string? Notes { get; set; }
}

public class CreateQualityInspectionItemRequest
{
    public int? GrnItemId { get; set; }

    [Required]
    public int ItemId { get; set; }

    public int? LotId { get; set; }

    public decimal DeliveredQuantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Accepted quantity cannot be negative.")]
    public decimal AcceptedQuantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Rejected quantity cannot be negative.")]
    public decimal RejectedQuantity { get; set; }

    public decimal ConcessionQuantity { get; set; }

    public string? DefectReason { get; set; }

    public string? Notes { get; set; }
}
