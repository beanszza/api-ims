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

    public int? DeliveryId { get; set; }
    public Delivery? Delivery { get; set; }

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public int ReceivingLocationId { get; set; }
    public Location ReceivingLocation { get; set; } = null!;

    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;

    public string DeliveryNoteNumber { get; set; } = string.Empty;

    public string? SupplierDrNumber { get; set; }

    public string? SupplierInvoiceNumber { get; set; }

    public string? ReceivingBay { get; set; }

    public string? Carrier { get; set; }

    public string ReceivedBy { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? PostedBy { get; set; }

    public DateTime? PostedAt { get; set; }

    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Draft;

    public string? RejectedBy { get; set; }

    public DateTime? RejectedAt { get; set; }

    public string? RejectionReason { get; set; }

    public string? Notes { get; set; }

    public ICollection<GoodsReceiptItem> Items { get; set; } = new List<GoodsReceiptItem>();

    public ICollection<Discrepancy> Discrepancies { get; set; } = new List<Discrepancy>();

    public ICollection<PutAwayTransaction> PutAways { get; set; } = new List<PutAwayTransaction>();

    public ICollection<StockIn> StockIns { get; set; } = new List<StockIn>();
}
