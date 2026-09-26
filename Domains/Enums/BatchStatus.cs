namespace Domains.Enums;

/// <summary>
/// Lifecycle of a production batch. Values match what the production UI filters on
/// (see <c>frontend-scms/components/pages/ViewProduction.tsx</c>).
/// </summary>
public enum BatchStatus
{
    /// <summary>Legacy rows created before a status was assigned.</summary>
    [DbValue("")]
    Unspecified = 0,

    [DbValue("Scheduled")]
    Scheduled = 1,

    [DbValue("In Progress")]
    InProgress = 2,

    /// <summary>Released by QA, awaiting packaging and posting to inventory.</summary>
    [DbValue("Passed QA")]
    PassedQa = 3,

    [DbValue("Rejected")]
    Rejected = 4,

    [DbValue("Completed")]
    Completed = 5,

    /// <summary>Finished goods have been posted to stock. Terminal.</summary>
    [DbValue("Inventory Added")]
    InventoryAdded = 6,

    [DbValue("Cancelled")]
    Cancelled = 7,

    [DbValue("Approved")]
    Approved = 8
}
