using System;
using System.Collections.Generic;
using Domains.Enums;

namespace Domains.Entities;

public class Delivery
{
    public int DeliveryId { get; set; }

    /// <summary>Human-readable document number, e.g. "DEL-2026-0001". Unique.</summary>
    public string DeliveryNumber { get; set; } = string.Empty;

    public int PoId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public int ReceivingLocationId { get; set; }
    public Location ReceivingLocation { get; set; } = null!;

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Scheduled;

    // Dates
    public DateTime ScheduledDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedArrivalDate { get; set; }
    public DateTime? DispatchedDate { get; set; }
    public DateTime? ActualArrivalDate { get; set; }

    // Logistics info
    public string? TrackingNumber { get; set; }
    public string? Carrier { get; set; }
    public string? DriverName { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public string? DeliveryNoteNumber { get; set; } // supplier's DR/waybill reference

    // Arrival condition
    public string? ArrivalCondition { get; set; } // "Normal", "Damaged", "Other"
    public string? ArrivalNotes { get; set; }

    // General
    public string? PaymentType { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentUrl { get; set; } // legacy / fallback
    public string? ScheduledAttachment { get; set; } // base64 proof at schedule stage
    public string? DispatchAttachment { get; set; } // base64 proof at dispatch stage
    public string? ArrivalAttachment { get; set; } // base64 proof at arrival stage
    public string CreatedBy { get; set; } = string.Empty;
    public string? DispatchedBy { get; set; }
    public string? ReceivedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<DeliveryItem> Items { get; set; } = new List<DeliveryItem>();
    public ICollection<GoodsReceipt> GoodsReceipts { get; set; } = new List<GoodsReceipt>();
}
