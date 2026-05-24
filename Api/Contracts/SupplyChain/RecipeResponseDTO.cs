namespace api_scm.Contracts.Responses;

public class RecipeResponse
{
    public int RecipeId { get; set; }
    public int ProductId { get; set; }
    public int OutputQuantity { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<RecipeIngredientResponse> Ingredients { get; set; } = new();
}

public class RecipeIngredientResponse
{
    public int IngredientId { get; set; }
    public int ItemId { get; set; }
    public int UomId { get; set; }
    public int StandardQuantity { get; set; }
}
