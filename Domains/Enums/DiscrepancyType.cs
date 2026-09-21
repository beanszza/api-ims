namespace Domains.Enums;

public enum DiscrepancyType
{
    Unspecified = 0,

    [DbValue("PartialShort")]
    PartialShort = 1,

    [DbValue("OverSupply")]
    OverSupply = 2,

    [DbValue("Rejected")]
    Rejected = 3
}
