namespace Domains.Enums;

public enum GoodsReceiptStatus
{
    Unspecified = 0,

    [DbValue("Draft")]
    Draft = 1,

    [DbValue("Received")]
    Received = 2,

    [DbValue("Inspected")]
    Inspected = 3,

    [DbValue("Cancelled")]
    Cancelled = 4
}
