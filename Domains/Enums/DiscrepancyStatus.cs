namespace Domains.Enums;

public enum DiscrepancyStatus
{
    Unspecified = 0,

    [DbValue("Open")]
    Open = 1,

    [DbValue("InReview")]
    InReview = 2,

    [DbValue("Resolved")]
    Resolved = 3,

    [DbValue("Closed")]
    Closed = 4
}
