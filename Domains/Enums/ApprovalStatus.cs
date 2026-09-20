namespace Domains.Enums;

public enum ApprovalStatus
{
    Unspecified = 0,

    [DbValue("Pending")]
    Pending = 1,

    [DbValue("Approved")]
    Approved = 2,

    [DbValue("Auto Approved", Aliases = ["AutoApproved"])]
    AutoApproved = 3,

    [DbValue("Rejected")]
    Rejected = 4,

    [DbValue("Withdrawn", Aliases = ["Cancelled"])]
    Withdrawn = 5
}
