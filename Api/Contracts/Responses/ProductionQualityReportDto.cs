using System.Collections.Generic;

namespace api_scm.Api.Contracts.Responses;

public class ProductionQualityReportResponseDto
{
    public IEnumerable<KitchenYieldEfficiencyDto> YieldEfficiency { get; set; } = new List<KitchenYieldEfficiencyDto>();
}

public class KitchenYieldEfficiencyDto
{
    public string RecipeName { get; set; } = string.Empty;
    public string TotalBatchesCooked { get; set; } = string.Empty;
    public string TotalOutputQty { get; set; } = string.Empty;
    public string YieldSuccessRate { get; set; } = string.Empty;
    public string TotalRejectedQty { get; set; } = string.Empty;
    public string IngredientWasteQty { get; set; } = string.Empty;
    public string CommonFailureReason { get; set; } = string.Empty;
}
