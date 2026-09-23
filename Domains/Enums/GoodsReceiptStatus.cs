namespace Domains.Enums;

public enum GoodsReceiptStatus
{
    Unspecified = 0,

    [DbValue("Draft")]
    Draft = 1,

    [DbValue("Received")]
    Received = 2,

    [DbValue("QaCompleted")]
    QaCompleted = 3,

    [DbValue("PartiallyPutAway")]
    PartiallyPutAway = 4,

    [DbValue("FullyPutAway")]
    FullyPutAway = 5,

    [DbValue("Cancelled")]
    Cancelled = 6,

    [DbValue("QaPending")]
    QaPending = 7,

    [DbValue("Rejected")]
    Rejected = 8
}
