using System;
using System.Collections.Generic;

namespace api_scm.Contracts.Responses;

public class InventoryValuationReportResponse
{
    public DateTime AsOfDate { get; set; }
    public decimal TotalValuation { get; set; }
    public int TotalLotsCount { get; set; }
    public List<CategoryValuationSummary> ByCategory { get; set; } = new();
    public List<LocationValuationSummary> ByLocation { get; set; } = new();
    public List<ItemValuationDetail> Items { get; set; } = new();
}

public class CategoryValuationSummary
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalValue { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public class LocationValuationSummary
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public decimal TotalValue { get; set; }
    public int LotsCount { get; set; }
}

public class ItemValuationDetail
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal OnHandQuantity { get; set; }
    public string UomName { get; set; } = string.Empty;
    public decimal MovingAverageUnitCost { get; set; }
    public decimal TotalValue { get; set; }
    public int LotsCount { get; set; }
}

public class CycleCountResponse
{
    public int CycleCountId { get; set; }
    public string CountNumber { get; set; } = string.Empty;
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public DateTime CountDate { get; set; }
    public DateTime? ReconciledDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CountedBy { get; set; } = string.Empty;
    public string? ReconciledBy { get; set; }
    public string? Notes { get; set; }
    public decimal TotalSystemValue { get; set; }
    public decimal TotalCountedValue { get; set; }
    public decimal TotalVarianceValue { get; set; }
    public List<CycleCountItemResponse> Items { get; set; } = new();
}

public class CycleCountItemResponse
{
    public int CycleCountItemId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int? LotId { get; set; }
    public string? LotCode { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal VarianceQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal VarianceCost { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

public class MrpPlanResponse
{
    public DateTime GeneratedAt { get; set; }
    public int PlanningHorizonDays { get; set; }
    public List<MrpMaterialRequirementDto> MaterialRequirements { get; set; } = new();
    public List<ReorderSuggestionDto> SuggestedReorders { get; set; } = new();
}

public class MrpMaterialRequirementDto
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal GrossRequirement { get; set; }
    public decimal CurrentStockOnHand { get; set; }
    public decimal OnOrderQuantity { get; set; }
    public decimal SafetyStockThreshold { get; set; }
    public decimal NetRequirement { get; set; }
    public string UomName { get; set; } = string.Empty;
}

public class ReorderSuggestionDto
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal SuggestedOrderQuantity { get; set; }
    public string UomName { get; set; } = string.Empty;
    public int? PreferredSupplierId { get; set; }
    public string? PreferredSupplierName { get; set; }
    public decimal EstimatedUnitCost { get; set; }
    public decimal EstimatedTotalCost { get; set; }
    public string UrgencyLevel { get; set; } = "Normal"; // Critical, High, Normal
}
