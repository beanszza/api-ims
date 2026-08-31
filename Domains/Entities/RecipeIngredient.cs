namespace Domains.Entities;

public class RecipeIngredient
{
    public int IngredientId { get; set; }
    public int RecipeId { get; set; }
    public int ItemId { get; set; }
    public int UomId { get; set; }

    /// <summary>
    /// Quantity required per single batch, expressed in <see cref="UomId"/>.
    /// </summary>
    /// <remarks>
    /// Task 4 adds the conversion needed to reconcile this unit against the item's stocking unit.
    /// Until then, an ingredient recorded in grams is still subtracted from a kilogram balance
    /// unit for unit.
    /// </remarks>
    public decimal StandardQuantity { get; set; }

    public Recipe? Recipe { get; set; }
    public Item? Item { get; set; }
    public UnitOfMeasure? Uom { get; set; }
}