namespace Domains.Enums;

public enum PutAwayStatus
{
    Unspecified = 0,

    [DbValue("Pending")]
    Pending = 1,

    [DbValue("Completed")]
    Completed = 2,

    [DbValue("Cancelled")]
    Cancelled = 3
}
