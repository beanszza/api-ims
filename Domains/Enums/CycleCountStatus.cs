namespace Domains.Enums;

public enum CycleCountStatus
{
    [DbValue("Draft")]
    Draft = 1,

    [DbValue("In Progress")]
    InProgress = 2,

    [DbValue("Completed")]
    Completed = 3,

    [DbValue("Reconciled")]
    Reconciled = 4,

    [DbValue("Cancelled")]
    Cancelled = 5
}
