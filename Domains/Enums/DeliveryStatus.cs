namespace Domains.Enums;

/// <summary>
/// Lifecycle of a delivery (shipment event) under a purchase order.
/// </summary>
public enum DeliveryStatus
{
    /// <summary>Shipment is planned/confirmed but supplier has not dispatched it yet.</summary>
    [DbValue("Scheduled")]
    Scheduled = 1,

    /// <summary>Supplier has dispatched it and it is traveling.</summary>
    [DbValue("In Transit")]
    InTransit = 2,

    /// <summary>Shipment has physically reached our facility. Ready for GRN.</summary>
    [DbValue("Arrived")]
    Arrived = 3,

    /// <summary>Shipment was cancelled before arrival.</summary>
    [DbValue("Cancelled")]
    Cancelled = 4
}
