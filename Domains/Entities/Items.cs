namespace Domains.Entities;

/// <summary>
/// Raw material / stock item. Per ERD, quantity by location is stored in <see cref="Inventory"/> rows (sum = total on hand).
/// </summary>
public class Item
{
    public int ItemId { get; set; }
    
    /// <summary>Unique human-readable identifier (e.g. SPL-2026-0001)</summary>
    public string ItemCode { get; set; } = string.Empty;
    
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Display unit shown in the UI.
    /// </summary>
    /// <remarks>
    /// Retained because the existing screens and DTOs bind to it. For anything arithmetic, use
    /// <see cref="StockUomId"/>: that is the unit balances are denominated in and the unit every
    /// conversion targets. The two are set together on create and only diverge deliberately.
    /// </remarks>
    public int UomId { get; set; }

    /// <summary>
    /// Authoritative stocking unit: the unit <see cref="Inventory.CurrentStock"/> is expressed in.
    /// </summary>
    /// <remarks>
    /// Every quantity arriving in another unit - a recipe written in grams, a supplier selling by the
    /// sack - is converted into this unit before it touches a balance.
    /// </remarks>
    public int StockUomId { get; set; }

    public int CategoryId { get; set; }

    /// <summary>Reorder trigger level, in the item's stocking unit of measure.</summary>
    public decimal MinStockLevel { get; set; }

    /// <summary>Maximum sensible holding, in the item's stocking unit of measure.</summary>
    public decimal MaxStockLevel { get; set; }

    public bool IsActive { get; set; }

    public UnitOfMeasure? Uom { get; set; }

    /// <summary>Navigation for <see cref="StockUomId"/>.</summary>
    public UnitOfMeasure? StockUom { get; set; }

    public Category? Category { get; set; }
    public ICollection<FinishedProduct> FinishedProducts { get; set; } = new List<FinishedProduct>();
    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<BatchConsumption> BatchConsumptions { get; set; } = new List<BatchConsumption>();
    public ICollection<InventoryMovementLog> InventoryMovementLogs { get; set; } = new List<InventoryMovementLog>();
    public ICollection<SupplierItem> SupplierItems { get; set; } = new List<SupplierItem>();
}
