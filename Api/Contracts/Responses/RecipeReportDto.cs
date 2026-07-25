using System.Collections.Generic;

namespace api_scm.Api.Contracts.Responses;

public class RecipeReportResponseDto
{
    public IEnumerable<RecipeReportItemDto> Recipes { get; set; } = new List<RecipeReportItemDto>();
}

public class RecipeReportItemDto
{
    public int RecipeNo { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public string FinishedProduct { get; set; } = string.Empty;
    public double TargetYield { get; set; }
    public int IngredientsCount { get; set; }
    public string Status { get; set; } = string.Empty;
}
