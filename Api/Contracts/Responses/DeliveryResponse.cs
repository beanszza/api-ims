using System;
using System.Collections.Generic;

namespace api_scm.Contracts.Responses;

public class DeliveryResponse
{
    public int DeliveryId { get; set; }
    public string DeliveryNumber { get; set; } = string.Empty;

    public int PoId { get; set; }
    public string PoNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>Supplier payment terms from the Purchase Order (read-only).</summary>
    public string PaymentType { get; set; } = string.Empty;

    public int ReceivingLocationId { get; set; }
    public string ReceivingLocationName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    // Dates
    public DateTime ScheduledDate { get; set; }
    public DateTime? ExpectedArrivalDate { get; set; }
    public DateTime? DispatchedDate { get; set; }
    public DateTime? ActualArrivalDate { get; set; }

    // Logistics info
    public string? TrackingNumber { get; set; }
    public string? Carrier { get; set; }
    public string? DriverName { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public string? DeliveryNoteNumber { get; set; }

    // Arrival condition
    public string? ArrivalCondition { get; set; }
    public string? ArrivalNotes { get; set; }

    // General
    public string? Notes { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? ScheduledAttachment { get; set; }
    public string? DispatchAttachment { get; set; }
    public string? ArrivalAttachment { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? DispatchedBy { get; set; }
    public string? ReceivedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    // Associated GRN (if any)
    public int? GrnId { get; set; }
    public string? GrnNumber { get; set; }

    public List<DeliveryItemResponse> Items { get; set; } = new();
}

public class DeliveryItemResponse
{
    public int DeliveryItemId { get; set; }
    public int PoItemId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;

    public decimal PoOrderedQuantity { get; set; }
    public decimal PoTotalReceivedQuantity { get; set; }
    public decimal PoOutstandingQuantity { get; set; }
    public decimal AlreadyScheduledQuantity { get; set; }

    /// <summary>Declared quantity for this specific delivery shipment.</summary>
    public decimal DeclaredQuantity { get; set; }

    public int PurchaseUomId { get; set; }
    public string PurchaseUomName { get; set; } = string.Empty;
}
