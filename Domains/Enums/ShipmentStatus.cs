namespace Domains.Enums;

/// <summary>
/// Lifecycle of a stock movement between locations. Values match what the distribution UI sends
/// (see <c>frontend-scms/components/StockTransferTable.tsx</c>).
/// </summary>
/// <remarks>
/// Reused by <c>Shipment</c> in Task 35 when transfers are replaced by trip-level shipments, which
/// is why it is named for shipments rather than transfers.
/// </remarks>
public enum ShipmentStatus
{
    /// <summary>Legacy rows created before a status was assigned.</summary>
    [DbValue("")]
    Unspecified = 0,

    /// <summary>Planned, nothing has left the source yet.</summary>
    [DbValue("Pending")]
    Pending = 1,

    /// <summary>Dispatched. Source is debited; stock is in transit.</summary>
    [DbValue("In Transit")]
    InTransit = 2,

    /// <summary>Receipt confirmed at the destination.</summary>
    [DbValue("Completed")]
    Completed = 3,

    [DbValue("Cancelled")]
    Cancelled = 4
}
