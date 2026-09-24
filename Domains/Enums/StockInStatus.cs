namespace Domains.Enums;

public enum StockInStatus
{
    Unspecified = 0,

    [DbValue("Draft")]
    Draft = 1,

    [DbValue("PendingApproval")]
    PendingApproval = 2,

    [DbValue("Approved")]
    Approved = 3,

    [DbValue("Rejected")]
    Rejected = 4,

    [DbValue("Committed")]
    Committed = 5
}
