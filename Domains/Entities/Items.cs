namespace Domains.Entities;

/// <summary>
/// Raw material / stock item. Per ERD, quantity by location is stored in <see cref="Inventory"/> rows (sum = total on hand).
/// </summary>
public class Item
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int UomId { get; set; }
    public int CategoryId { get; set; }
    public int MinStockLevel { get; set; }
    public int MaxStockLevel { get; set; }
    public bool IsActive { get; set; }

    public UnitOfMeasure? Uom { get; set; }
    public Category? Category { get; set; }
    public ICollection<FinishedProduct> FinishedProducts { get; set; } = new List<FinishedProduct>();
    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<BatchConsumption> BatchConsumptions { get; set; } = new List<BatchConsumption>();
    public ICollection<InventoryMovementLog> InventoryMovementLogs { get; set; } = new List<InventoryMovementLog>();
}
