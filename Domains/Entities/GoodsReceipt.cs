using System;
using System.Collections.Generic;
using Domains.Enums;

namespace Domains.Entities;

public class GoodsReceipt
{
    public int GrnId { get; set; }

    /// <summary>Canonical human-readable GRN number, e.g. "GRN-2026-0001".</summary>
    public string GrnNumber { get; set; } = string.Empty;

    public int PoId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public int ReceivingLocationId { get; set; }
    public Location ReceivingLocation { get; set; } = null!;

    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;

    public string DeliveryNoteNumber { get; set; } = string.Empty;

    public string? Carrier { get; set; }

    public string ReceivedBy { get; set; } = string.Empty;

    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Received;

    public string? Notes { get; set; }

    public ICollection<GoodsReceiptItem> Items { get; set; } = new List<GoodsReceiptItem>();
}
