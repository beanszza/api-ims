namespace api_scm.Contracts.Requests;

public class CreateRecipeRequest
{
    public int ProductId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public int OutputQuantity { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<CreateRecipeIngredientRequest> Ingredients { get; set; } = new();
}

public class CreateRecipeIngredientRequest
{
    public int ItemId { get; set; }
    public int UomId { get; set; }
    public int StandardQuantity { get; set; }
}
