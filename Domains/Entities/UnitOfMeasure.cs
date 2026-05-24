namespace Domains.Entities;

public class UnitOfMeasure
{
    public int UomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;

    public ICollection<Item> Items { get; set; } = new List<Item>();
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}

