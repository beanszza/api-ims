namespace api_scm.Contracts.Responses;

public class RecipeResponse
{
    public int RecipeId { get; set; }
    public string RecipeCode { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public decimal OutputQuantity { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<RecipeIngredientResponse> Ingredients { get; set; } = new();
}

public class RecipeIngredientResponse
{
    public int IngredientId { get; set; }
    public int ItemId { get; set; }
    public int UomId { get; set; }
    public decimal StandardQuantity { get; set; }
}
