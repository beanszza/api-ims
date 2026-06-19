namespace Domains.Entities;

public class ProductionBatch
{
    public int BatchId { get; set; }
    public int RecipeId { get; set; }
    public int ProductId { get; set; }
    public int BatchMultiplier { get; set; }
    public int EstimatedQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public DateTime ProductionDate { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = "Scheduled";
    public string AssignedCook { get; set; } = string.Empty;
    public string QualityStatus { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public Recipe? Recipe { get; set; }
    public FinishedProduct? Product { get; set; }
    public ICollection<BatchConsumption> BatchConsumptions { get; set; } = new List<BatchConsumption>();
}