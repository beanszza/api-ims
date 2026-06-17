namespace Domains.Entities;

public class PurchaseOrder
{
    public int PoId { get; set; }
    public int SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime ExpectedArrivalDate { get; set; }
    public string Status { get; set; } = string.Empty;
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