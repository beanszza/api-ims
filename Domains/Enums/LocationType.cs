namespace Domains.Enums;

/// <summary>
/// Role a location plays, which determines what may be posted into or out of it.
/// </summary>
/// <remarks>
/// <c>Location.LocationType</c> is currently free text and holds ad-hoc values such as "Storage"
/// (invented by the receiving fallback in <c>PurchaseOrderService</c>). Those are kept as read
/// aliases. The column is converted, and system locations seeded, in Task 7.
/// </remarks>
public enum LocationType
{
    /// <summary>Legacy rows with no type recorded.</summary>
    [DbValue("")]
    Unspecified = 0,

    /// <summary>General raw material and packaging store.</summary>
    [DbValue("Warehouse", Aliases = ["Storage"])]
    Warehouse = 1,

    /// <summary>Production floor. The only valid source for batch consumption.</summary>
    [DbValue("Production")]
    Production = 2,

    /// <summary>Work in progress: holds material that has left store but is not yet finished goods.</summary>
    [DbValue("WIP")]
    Wip = 3,

    /// <summary>Received-but-uninspected and rejected stock. Visible, never usable.</summary>
    [DbValue("Quarantine")]
    Quarantine = 4,

    /// <summary>Finished goods store.</summary>
    [DbValue("Finished Goods", Aliases = ["FinishedGoods"])]
    FinishedGoods = 5,

    /// <summary>Holds dispatched stock until the destination confirms receipt.</summary>
    [DbValue("In Transit")]
    InTransit = 6,

    /// <summary>A retail branch or store.</summary>
    [DbValue("Branch")]
    Branch = 7,

    /// <summary>Temporary selling point. Seeded by <c>Program.cs</c> as "Bazaar Booth - SM North".</summary>
    [DbValue("Bazaar")]
    Bazaar = 9,

    /// <summary>Terminal location for written-off stock, kept so disposals stay auditable.</summary>
    [DbValue("Disposal")]
    Disposal = 8
}
