using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreateDeliveryRequest
{
    [Required]
    public int PoId { get; set; }

    public DateTime? ScheduledDate { get; set; }

    public DateTime? ExpectedArrivalDate { get; set; }

    public string? TrackingNumber { get; set; }

    public string? Carrier { get; set; }

    public string? DriverName { get; set; }

    public string? VehiclePlateNumber { get; set; }

    public string? DeliveryNoteNumber { get; set; }

    public string? PaymentType { get; set; }

    public string? Notes { get; set; }

    public string? AttachmentUrl { get; set; }

    public string? ScheduledAttachmentBase64 { get; set; }

    [Required]
    public List<CreateDeliveryItemRequest> Items { get; set; } = new();
}

public class CreateDeliveryItemRequest
{
    [Required]
    public int PoItemId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Shipment quantity must be greater than zero.")]
    public decimal DeclaredQuantity { get; set; }
}

public class MarkDispatchedRequest
{
    public DateTime? DispatchedDate { get; set; }

    public DateTime? ExpectedArrivalDate { get; set; }

    public string? TrackingNumber { get; set; }

    public string? Carrier { get; set; }

    public string? DriverName { get; set; }

    public string? VehiclePlateNumber { get; set; }

    public string? Notes { get; set; }

    public string? AttachmentUrl { get; set; }

    public string? DispatchAttachmentBase64 { get; set; }
}

public class MarkArrivedRequest
{
    public DateTime? ActualArrivalDate { get; set; }

    public string? ArrivalCondition { get; set; }

    public string? ArrivalNotes { get; set; }

    public string? DeliveryNoteNumber { get; set; }

    public string? Notes { get; set; }

    public string? AttachmentUrl { get; set; }

    public string? ReceivedBy { get; set; }

    public string? ArrivalAttachmentBase64 { get; set; }
}

public class CancelDeliveryRequest
{
    public string? Reason { get; set; }
}

