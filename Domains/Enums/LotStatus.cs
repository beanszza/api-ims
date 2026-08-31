namespace Domains.Enums;

/// <summary>
/// State of a stock lot. Only <see cref="Available"/> counts toward usable on-hand quantity.
/// </summary>
/// <remarks>Introduced with <c>InventoryLot</c> in Task 8.</remarks>
public enum LotStatus
{
    /// <summary>Physically received but not yet inspected. Visible, not usable.</summary>
    [DbValue("Quarantine")]
    Quarantine = 1,

    /// <summary>Released by QA and free to consume or ship.</summary>
    [DbValue("Available")]
    Available = 2,

    /// <summary>Blocked, typically by a recall investigation. Visible, not usable.</summary>
    [DbValue("On Hold")]
    OnHold = 3,

    /// <summary>Past its expiry date. Removed from available stock automatically.</summary>
    [DbValue("Expired")]
    Expired = 4,

    /// <summary>Fully drawn down to zero remaining.</summary>
    [DbValue("Consumed")]
    Consumed = 5,

    /// <summary>Failed incoming QA, awaiting return or disposal.</summary>
    [DbValue("Rejected")]
    Rejected = 6,

    /// <summary>Sent back to the supplier.</summary>
    [DbValue("Returned")]
    Returned = 7,

    /// <summary>Written off. Terminal.</summary>
    [DbValue("Disposed")]
    Disposed = 8
}
