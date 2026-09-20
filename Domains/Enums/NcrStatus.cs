namespace Domains.Enums;

public enum NcrStatus
{
    Unspecified = 0,

    [DbValue("Open")]
    Open = 1,

    [DbValue("Under Review", Aliases = ["Investigating"])]
    UnderReview = 2,

    [DbValue("Resolved")]
    Resolved = 3,

    [DbValue("Closed")]
    Closed = 4
}
