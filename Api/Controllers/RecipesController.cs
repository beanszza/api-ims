using api_scm.Contracts.Requests;
using api_scm.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Applications.Interfaces;
using System.Threading.Tasks;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecipesController : ControllerBase
{
    private readonly IRecipeService _recipeService;

    public RecipesController(IRecipeService recipeService)
    {
        _recipeService = recipeService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<RecipeResponse>>> CreateRecipe([FromBody] CreateRecipeRequest request)
    {
        var result = await _recipeService.CreateRecipeAsync(request);
        return result.Success
            ? Ok(result) // Or CreatedAtRoute if we had a Get method
            : StatusCode(400, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<RecipeResponse>>> UpdateRecipe([FromRoute] int id, [FromBody] UpdateRecipeRequest request)
    {
        var result = await _recipeService.UpdateRecipeAsync(id, request);
        return result.Success ? Ok(result) : StatusCode(400, result);
    }
}
