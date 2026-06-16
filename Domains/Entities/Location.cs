namespace Domains.Entities;

public class Location
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string LocationType { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";

    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
    public ICollection<InventoryMovementLog> InventoryMovementLogs { get; set; } = new List<InventoryMovementLog>();
}