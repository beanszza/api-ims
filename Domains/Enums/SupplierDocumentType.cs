namespace Domains.Enums;

public enum SupplierDocumentType
{
    Unspecified = 0,

    [DbValue("FDA LTO", Aliases = ["LTO", "License to Operate"])]
    FdaLto = 1,

    [DbValue("Sanitary Permit", Aliases = ["Sanitary"])]
    SanitaryPermit = 2,

    [DbValue("Business Permit", Aliases = ["Mayor's Permit"])]
    BusinessPermit = 3,

    [DbValue("Certificate of Analysis", Aliases = ["COA"])]
    Coa = 4,

    [DbValue("HACCP Certification", Aliases = ["HACCP"])]
    HaccpCert = 5,

    [DbValue("Halal Certification", Aliases = ["Halal"])]
    HalalCert = 6,

    [DbValue("Other")]
    Other = 7
}
