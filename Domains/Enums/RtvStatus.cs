namespace Domains.Enums;

public enum RtvStatus
{
    Unspecified = 0,

    [DbValue("Pending Dispatch")]
    PendingDispatch = 1,

    [DbValue("Dispatched")]
    Dispatched = 2,

    [DbValue("Credit Note Received", Aliases = ["Credited", "Settled"])]
    CreditNoteReceived = 3,

    [DbValue("Cancelled")]
    Cancelled = 4,

    [DbValue("Pending Approval")]
    PendingApproval = 5
}
