using Domains.Enums;

namespace Domains.Entities;

public class PurchaseOrder
{
    public int PoId { get; set; }

    /// <summary>
    /// Human-readable document number, for example <c>PO-2026-0042</c>. Unique.
    /// </summary>
    /// <remarks>
    /// The number people quote. Previously the UI derived a label from the primary key, which meant the
    /// reference nobody could look up in a supplier's email was tied to a surrogate id.
    /// </remarks>
    public string PoNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime ExpectedArrivalDate { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Pending;
    public string PaymentType { get; set; } = string.Empty;
    public string ProofImageUrl { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }

    public string? QaNotes { get; set; }
    public DateTime? QaInspectedDate { get; set; }
    public string? QaStatus { get; set; }
    public string? InspectedBy { get; set; }

    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
}