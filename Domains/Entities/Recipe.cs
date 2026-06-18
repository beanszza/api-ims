namespace Domains.Entities;

public class Recipe
{
    public int RecipeId { get; set; }
    public int ProductId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public int OutputQuantity { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public FinishedProduct? Product { get; set; }
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<ProductionBatch> ProductionBatches { get; set; } = new List<ProductionBatch>();
}