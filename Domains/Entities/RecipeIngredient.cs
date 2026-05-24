namespace Domains.Entities;

public class RecipeIngredient
{
    public int IngredientId { get; set; }
    public int RecipeId { get; set; }
    public int ItemId { get; set; }
    public int UomId { get; set; }
    public int StandardQuantity { get; set; }

    public Recipe? Recipe { get; set; }
    public Item? Item { get; set; }
    public UnitOfMeasure? Uom { get; set; }
}