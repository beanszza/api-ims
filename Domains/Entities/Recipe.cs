using System.Collections.Generic;

namespace Domains.Entities;

public class Recipe
{
    public int RecipeId { get; set; }
    
    /// <summary>Unique human-readable identifier (e.g. BOM-2026-0001)</summary>
    public string RecipeCode { get; set; } = string.Empty;
    
    public int ProductId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public decimal OutputQuantity { get; set; }
    public int? YieldUomId { get; set; }
    public string Status { get; set; } = "Approved"; // Draft, UnderReview, Approved, Archived
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public FinishedProduct? Product { get; set; }
    public UnitOfMeasure? YieldUom { get; set; }
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<ProductionBatch> ProductionBatches { get; set; } = new List<ProductionBatch>();
}