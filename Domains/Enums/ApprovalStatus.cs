namespace Domains.Enums;

/// <summary>
/// State of an approval request. Shared by purchase order approval, QA release and disposal sign-off.
/// </summary>
/// <remarks>Introduced with the approval engine in Task 15.</remarks>
public enum ApprovalStatus
{
    /// <summary>Awaiting a decision. The underlying document cannot advance.</summary>
    [DbValue("Pending")]
    Pending = 1,

    /// <summary>Approved by a person, recorded with their identity and timestamp.</summary>
    [DbValue("Approved")]
    Approved = 2,

    /// <summary>Below the configured threshold, so no human decision was required.</summary>
    [DbValue("Auto Approved")]
    AutoApproved = 3,

    [DbValue("Rejected")]
    Rejected = 4,

    /// <summary>Withdrawn by the requester before a decision was made.</summary>
    [DbValue("Withdrawn")]
    Withdrawn = 5
}
