namespace Domains.Entities;

public class Inventory
{
    public int InventoryId { get; set; }
    public int? DriverId { get; set; }
    public int ItemId { get; set; }
    public int LocationId { get; set; }
    public int CurrentStock { get; set; }

    public Driver? Driver { get; set; }
    public Item? Item { get; set; }
    public Location? Location { get; set; }
}