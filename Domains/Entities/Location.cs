using Domains.Enums;

namespace Domains.Entities;

/// <summary>
/// A place stock can be held.
/// </summary>
public class Location
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;

    /// <summary>
    /// The role this location plays, which decides what may be posted into or out of it.
    /// </summary>
    /// <remarks>
    /// Previously free text, holding ad-hoc values like "Storage" invented by the receiving fallback.
    /// Now a closed set, so code can ask "is this a production floor?" instead of matching on a name.
    /// </remarks>
    public LocationType LocationType { get; set; } = LocationType.Unspecified;

    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = "Active";

    /// <summary>
    /// True for locations the system depends on by role rather than by name.
    /// </summary>
    /// <remarks>
    /// Receiving, production, WIP, quarantine, finished goods and disposal are all resolved by looking
    /// up the system location of the matching type. They cannot be deleted, because deleting one would
    /// silently break posting rather than produce an obvious error.
    /// </remarks>
    public bool IsSystemLocation { get; set; }

    /// <summary>
    /// Optional parent, for modelling a bay inside a warehouse or an in-transit lane belonging to a branch.
    /// </summary>
    public int? ParentLocationId { get; set; }

    public Location? ParentLocation { get; set; }

    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
    public ICollection<InventoryMovementLog> InventoryMovementLogs { get; set; } = new List<InventoryMovementLog>();
}
