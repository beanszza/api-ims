using System;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class ResolveDiscrepancyRequest
{
    [Required]
    public string ResolutionType { get; set; } = string.Empty; // NewDelivery, CloseRemaining, LossReport, ReturnToSupplier, KeepWithCredit

    public string? ResolutionNotes { get; set; }
}

public class CreateLossReportRequest
{
    [Required]
    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }

    [Required]
    public string AuthorisedBy { get; set; } = string.Empty;
}

public class CreateRtvFromDiscrepancyRequest
{
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
}
