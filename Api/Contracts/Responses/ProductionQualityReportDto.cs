using System.Collections.Generic;

namespace api_scm.Api.Contracts.Responses;

public class ProductionQualityReportResponseDto
{
    public ProductionSummaryDto Summary { get; set; } = new ProductionSummaryDto();
    public IEnumerable<KitchenYieldEfficiencyDto> YieldEfficiency { get; set; } = new List<KitchenYieldEfficiencyDto>();
}

public class ProductionSummaryDto
{
    public int TotalBatches { get; set; }
    public int ScheduledBatches { get; set; }
    public int InProgressBatches { get; set; }
    public int PassedQaBatches { get; set; }
    public int RejectedBatches { get; set; }
    public string MostProducedItem { get; set; } = "N/A";
    public string SeldomProducedItem { get; set; } = "N/A";
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
