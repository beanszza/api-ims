using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;

namespace Applications.Interfaces;

public interface IRecipeService
{
    Task<ApiResponse<IEnumerable<RecipeResponse>>> GetAllRecipesAsync();
    Task<ApiResponse<RecipeResponse>> CreateRecipeAsync(CreateRecipeRequest request);
    Task<ApiResponse<RecipeResponse>> UpdateRecipeAsync(int id, UpdateRecipeRequest request);
}
