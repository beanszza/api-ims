namespace Domains.Entities;

public class Inventory
{
    public int InventoryId { get; set; }
    public int? DriverId { get; set; }
    public int ItemId { get; set; }
    public int LocationId { get; set; }

    /// <summary>
    /// On-hand quantity in the item's stocking unit of measure.
    /// </summary>
    /// <remarks>
    /// decimal(18,3) rather than int: ingredients are weighed, and a recipe calling for 0.75 kg of
    /// sugar or 2.5 L of water was previously unrepresentable. Three decimal places covers grams
    /// expressed in kilograms and millilitres expressed in litres.
    /// <para>
    /// As of Task 10 this is a cached projection of <c>SUM(InventoryLot.QuantityRemaining) WHERE
    /// Status = 'Available'</c> for this item/location, maintained solely by
    /// <c>IStockPostingService</c>. The lot table is now the source of truth; this column exists so the
    /// screens and reports that read a single on-hand number do not all need rewriting in the same
    /// task that introduced lots. Code paths not yet migrated onto <c>IStockPostingService</c>
    /// (Tasks 14, 17-18, 28, 36-37) still write it directly.
    /// </para>
    /// </remarks>
    public decimal CurrentStock { get; set; }

    public Driver? Driver { get; set; }
    public Item? Item { get; set; }
    public Location? Location { get; set; }
}