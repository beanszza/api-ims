using System;
using System.Collections.Generic;
using Domains.Enums;

namespace Domains.Entities;

public class ProductionBatch
{
    public int BatchId { get; set; }

    /// <summary>Canonical human-readable batch manufacturing number, e.g. "MB-2026-0001".</summary>
    public string BatchNumber { get; set; } = string.Empty;

    public int RecipeId { get; set; }
    public int ProductId { get; set; }

    /// <summary>How many standard recipe batches this run represents.</summary>
    public decimal BatchMultiplier { get; set; }

    /// <summary>Planned output: recipe output quantity times <see cref="BatchMultiplier"/>.</summary>
    public decimal EstimatedQuantity { get; set; }

    /// <summary>Output actually produced, recorded during the run.</summary>
    public decimal ActualQuantity { get; set; }

    public decimal ScrapQuantity { get; set; }
    public string? ScrapReason { get; set; }

    public decimal TotalMaterialCost { get; set; }
    public decimal UnitCost { get; set; }
    public decimal YieldPercentage { get; set; }

    public int? FgLotId { get; set; }
    public InventoryLot? FgLot { get; set; }

    public DateTime ProductionDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public ProductionStage Stage { get; set; } = ProductionStage.Preparation;
    public BatchStatus Status { get; set; } = BatchStatus.Scheduled;
    public string AssignedCook { get; set; } = string.Empty;
    public QcStatus QualityStatus { get; set; } = QcStatus.Pending;
    public string RejectionReason { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public Recipe? Recipe { get; set; }
    public FinishedProduct? Product { get; set; }
    public ICollection<BatchConsumption> BatchConsumptions { get; set; } = new List<BatchConsumption>();
}