using Domains.Enums;

namespace Domains.Entities;

public class PurchaseOrder
{
    public int PoId { get; set; }

    /// <summary>
    /// Human-readable document number, for example <c>PO-2026-0042</c>. Unique.
    /// </summary>
    public string PoNumber { get; set; } = string.Empty;

    /// <summary>
    /// Optional reference to the Purchase Requisition this PO was created from.
    /// </summary>
    public int? PrId { get; set; }
    public PurchaseRequisition? PurchaseRequisition { get; set; }

    public int SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime ExpectedArrivalDate { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public string PaymentType { get; set; } = string.Empty;
    public string ProofImageUrl { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }

    /// <summary>Name of the person who created / requested this PO.</summary>
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>Admin notes for rejection or return-for-revision reason.</summary>
    public string? AdminNotes { get; set; }

    public string? QaNotes { get; set; }
    public DateTime? QaInspectedDate { get; set; }
    public string? QaStatus { get; set; }
    public string? InspectedBy { get; set; }

    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
}