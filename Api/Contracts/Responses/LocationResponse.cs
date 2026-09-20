namespace api_scm.Contracts.Responses;

public class LocationResponse
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;

    /// <summary>
    /// Role this location plays: Warehouse, Production, WIP, Quarantine, Finished Goods, In Transit,
    /// Branch, Bazaar or Disposal. Sent as a string so the existing UI keeps working unchanged.
    /// </summary>
    public string LocationType { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// True when the system depends on this location by role. The UI should not offer to delete these.
    /// </summary>
    public bool IsSystemLocation { get; set; }

    /// <summary>Parent location, for a bay inside a warehouse or a branch's in-transit lane.</summary>
    public int? ParentLocationId { get; set; }
}
