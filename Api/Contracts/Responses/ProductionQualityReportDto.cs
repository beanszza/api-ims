namespace api_scm.Api.Contracts.Responses;

public class ProductionQualityReportDto
{
    public int BatchId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public double YieldSuccessRate { get; set; }
    public double IngredientWaste { get; set; }
    public int TotalRejectedQuantity { get; set; }
    public string CommonFailureReason { get; set; } = string.Empty;
    public System.DateTime ProductionDate { get; set; }
}
