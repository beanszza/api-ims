namespace Domains.Enums;

public enum QualityInspectionStatus
{
    Unspecified = 0,

    [DbValue("Pending")]
    Pending = 1,

    [DbValue("Passed")]
    Passed = 2,

    [DbValue("Passed with Concession", Aliases = ["Concession"])]
    PassedWithConcession = 3,

    [DbValue("Failed", Aliases = ["Rejected"])]
    Failed = 4
}
