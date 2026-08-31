namespace Domains.Enums;

/// <summary>
/// Lifecycle of a purchase order. Values match the strings the procurement UI already sends
/// (see <c>frontend-scms/components/orders-procurement/types.ts</c>).
/// </summary>
/// <remarks>
/// Task 14 expands this into Draft / PendingApproval / Approved / Sent / PartiallyReceived /
/// Received / Closed once receiving becomes a separate posted document.
/// </remarks>
public enum PurchaseOrderStatus
{
    /// <summary>Legacy rows created before a status was assigned.</summary>
    [DbValue("")]
    Unspecified = 0,

    /// <summary>Raised and sent to the supplier, nothing delivered yet.</summary>
    [DbValue("Pending")]
    Pending = 1,

    /// <summary>Delivery is physically here and awaiting QA inspection.</summary>
    [DbValue("Arrived")]
    Arrived = 2,

    /// <summary>Passed QA and posted to inventory.</summary>
    [DbValue("Completed")]
    Completed = 3,

    /// <summary>Failed QA.</summary>
    [DbValue("Rejected")]
    Rejected = 4,

    /// <summary>Called off before delivery.</summary>
    [DbValue("Cancelled")]
    Cancelled = 5
}
