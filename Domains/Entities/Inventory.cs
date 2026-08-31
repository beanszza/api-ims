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
    /// From Task 10 this becomes a cached projection of the lot ledger rather than the source of
    /// truth, maintained solely by <c>IStockPostingService</c>.
    /// </para>
    /// </remarks>
    public decimal CurrentStock { get; set; }

    public Driver? Driver { get; set; }
    public Item? Item { get; set; }
    public Location? Location { get; set; }
}