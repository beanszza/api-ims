namespace Domains.Entities;

public class Driver
{
    public int DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;

    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
}