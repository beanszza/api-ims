namespace Domains.Enums;

public enum PurchaseRequisitionStatus
{
    Unspecified = 0,

    [DbValue("Draft")]
    Draft = 1,

    [DbValue("Pending Approval", Aliases = ["Pending"])]
    PendingApproval = 2,

    [DbValue("Approved")]
    Approved = 3,

    [DbValue("Rejected")]
    Rejected = 4,

    [DbValue("Converted to PO", Aliases = ["Converted"])]
    ConvertedToPo = 5,

    [DbValue("Cancelled")]
    Cancelled = 6
}
