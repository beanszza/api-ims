namespace Api.Contracts.Production;

public class CreateProductionBatchRequest
{
    public int RecipeId { get; set; }
    public int ProductId { get; set; }
    public int BatchMultiplier { get; set; }
    public DateTime ScheduleDate { get; set; }
    public string AssignedCook { get; set; } = string.Empty;
}

public class ProductionBatchResponse
{
    public int BatchId { get; set; }
    public int RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int BatchMultiplier { get; set; }
    public int EstimatedQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public DateTime ProductionDate { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AssignedCook { get; set; } = string.Empty;
    public string QualityStatus { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class UpdateStageRequest
{
    public string Stage { get; set; } = string.Empty;
}

public class UpdateQaNotesRequest
{
    public string Notes { get; set; } = string.Empty;
}

public class UpdateQaApprovalRequest
{
    public bool IsApproved { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
    public int LocationId { get; set; } // Where finished goods will be stored
}

public class DashboardSummaryResponse
{
    public int ActiveBatches { get; set; }
    public int DelayedBatches { get; set; }
    public int PassedQaBatches { get; set; }
    public int CompletedBatches { get; set; }
}

public class LowStockAlertResponse
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinStockLevel { get; set; }
}
