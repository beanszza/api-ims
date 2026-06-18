using System.Collections.Generic;

namespace api_scm.Contracts.Requests;

public class UpdateRecipeRequest
{
    public string? RecipeName { get; set; }
    public int? OutputQuantity { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
    public List<CreateRecipeIngredientRequest>? Ingredients { get; set; }
}
