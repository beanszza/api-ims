using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreateCycleCountRequest
{
    [Required]
    public int LocationId { get; set; }

    public string? Notes { get; set; }

    [Required]
    public List<CreateCycleCountItemDto> Items { get; set; } = new();
}

public class CreateCycleCountItemDto
{
    [Required]
    public int ItemId { get; set; }

    public int? LotId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Counted quantity must be non-negative.")]
    public decimal CountedQuantity { get; set; }

    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

public class ReconcileCycleCountRequest
{
    public string? ReconciliationNotes { get; set; }
}

public class GenerateMrpPlanRequest
{
    /// <summary>Planning horizon in days (default: 30 days).</summary>
    public int PlanningHorizonDays { get; set; } = 30;

    /// <summary>Target finished goods production batches to plan for.</summary>
    public List<TargetProductionPlanDto> PlannedProductions { get; set; } = new();
}

public class TargetProductionPlanDto
{
    [Required]
    public int RecipeId { get; set; }

    [Range(1, int.MaxValue)]
    public int PlannedBatchesCount { get; set; } = 1;
}
