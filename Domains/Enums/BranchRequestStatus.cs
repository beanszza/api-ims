namespace Domains.Enums;

public enum BranchRequestStatus
{
    [DbValue("Pending")]
    Pending = 1,

    [DbValue("Approved")]
    Approved = 2,

    [DbValue("Dispatched")]
    Dispatched = 3,

    [DbValue("Fulfilled")]
    Fulfilled = 4,

    [DbValue("Rejected")]
    Rejected = 5
}
