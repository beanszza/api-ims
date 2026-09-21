namespace Domains.Enums;

public enum LossReportStatus
{
    Unspecified = 0,

    [DbValue("Draft")]
    Draft = 1,

    [DbValue("Authorised")]
    Authorised = 2,

    [DbValue("Posted")]
    Posted = 3
}
