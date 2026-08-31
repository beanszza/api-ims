using Domains.Enums;

namespace Domains.Entities;

public class ProductionBatch
{
    public int BatchId { get; set; }
    public int RecipeId { get; set; }
    public int ProductId { get; set; }
    /// <summary>
    /// How many standard recipe batches this run represents.
    /// </summary>
    /// <remarks>
    /// Decimal so that a half batch is expressible; the kitchen does not always cook whole multiples.
    /// </remarks>
    public decimal BatchMultiplier { get; set; }

    /// <summary>Planned output: recipe output quantity times <see cref="BatchMultiplier"/>.</summary>
    public decimal EstimatedQuantity { get; set; }

    /// <summary>Output actually produced, recorded during the run.</summary>
    public decimal ActualQuantity { get; set; }
    public DateTime ProductionDate { get; set; }
    public ProductionStage Stage { get; set; } = ProductionStage.Unspecified;
    public BatchStatus Status { get; set; } = BatchStatus.Scheduled;
    public string AssignedCook { get; set; } = string.Empty;
    public QcStatus QualityStatus { get; set; } = QcStatus.Unspecified;
    public string RejectionReason { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public Recipe? Recipe { get; set; }
    public FinishedProduct? Product { get; set; }
    public ICollection<BatchConsumption> BatchConsumptions { get; set; } = new List<BatchConsumption>();
}