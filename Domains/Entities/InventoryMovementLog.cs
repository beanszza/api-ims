namespace Domains.Entities;


public class InventoryMovementLog
{
    public int MovementId { get; set; }
    public int ItemId { get; set; }
    public int LocationId { get; set; }
    public int ChangeQuantity { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime Timestamp { get; set; }

    public Item? Item { get; set; }
    public Location? Location { get; set; }
}