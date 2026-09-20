namespace Domains.Enums;

/// <summary>
/// Lifecycle of a purchase order. Values match the strings the procurement UI already sends
/// (see <c>frontend-scms/components/orders-procurement/types.ts</c>).
/// </summary>
public enum PurchaseOrderStatus
{
    /// <summary>Legacy rows created before a status was assigned.</summary>
    [DbValue("")]
    Unspecified = 0,

    /// <summary>Raised and sent to the supplier, nothing delivered yet (legacy).</summary>
    [DbValue("Pending")]
    Pending = 1,

    /// <summary>Delivery is physically here and awaiting QA inspection (legacy).</summary>
    [DbValue("Arrived")]
    Arrived = 2,

    /// <summary>Passed QA and posted to inventory (legacy).</summary>
    [DbValue("Completed")]
    Completed = 3,

    /// <summary>Admin rejected the PO with a reason.</summary>
    [DbValue("Rejected")]
    Rejected = 4,

    /// <summary>Cancelled before delivery.</summary>
    [DbValue("Cancelled")]
    Cancelled = 5,

    /// <summary>PO saved as draft, not yet submitted for approval.</summary>
    [DbValue("Draft")]
    Draft = 6,

    /// <summary>PO submitted and awaiting admin approval.</summary>
    [DbValue("Pending Approval")]
    PendingApproval = 7,

    /// <summary>Admin returned the PO for revision with a reason.</summary>
    [DbValue("Returned")]
    Returned = 8,

    /// <summary>PO approved by admin — ready to be marked as sent to supplier.</summary>
    [DbValue("Approved")]
    Approved = 9,

    /// <summary>PO has been sent/confirmed to the supplier (Ordered).</summary>
    [DbValue("Ordered")]
    Ordered = 10,
}
