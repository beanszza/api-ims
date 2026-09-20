namespace Domains.Enums;

public enum InspectionType
{
    Unspecified = 0,

    [DbValue("Incoming", Aliases = ["Incoming QA", "Raw Materials"])]
    Incoming = 1,

    [DbValue("In Process", Aliases = ["WIP"])]
    InProcess = 2,

    [DbValue("Finished Goods", Aliases = ["FG QA"])]
    FinishedGoods = 3
}
