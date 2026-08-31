namespace Api.Contracts.Production;

public class CreateProductionBatchRequest
{
    public int RecipeId { get; set; }
    public int ProductId { get; set; }

    /// <summary>How many standard recipe batches to cook. Decimal so a half batch is allowed.</summary>
    public decimal BatchMultiplier { get; set; }

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
    public decimal BatchMultiplier { get; set; }
    public decimal EstimatedQuantity { get; set; }
    public decimal ActualQuantity { get; set; }
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
    public decimal? ActualQuantity { get; set; }
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
    public decimal CurrentStock { get; set; }
    public decimal MinStockLevel { get; set; }
}
