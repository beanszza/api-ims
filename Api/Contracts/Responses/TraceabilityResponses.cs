using System;
using System.Collections.Generic;

namespace api_scm.Contracts.Responses;

public class ForwardTraceResponse
{
    public LotSummary SourceLot { get; set; } = null!;
    public SupplierTraceSummary? SupplierOrigin { get; set; }
    public GrnTraceSummary? GoodsReceiptOrigin { get; set; }
    public List<BatchTraceSummary> AffectedProductionBatches { get; set; } = new();
    public List<FinishedGoodsTraceSummary> AffectedFinishedGoodsLots { get; set; } = new();
    public List<ShipmentTraceSummary> DownstreamBranchShipments { get; set; } = new();
}

public class BackwardTraceResponse
{
    public LotSummary FinishedGoodsLot { get; set; } = null!;
    public BatchTraceSummary? ProductionBatch { get; set; }
    public List<IngredientLotTraceSummary> ConsumedIngredientsAndPackaging { get; set; } = new();
}

public class LotSummary
{
    public int LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public decimal QuantityReceived { get; set; }
    public decimal QuantityRemaining { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public DateTime ReceivedDate { get; set; }
    public decimal UnitCost { get; set; }
}

public class SupplierTraceSummary
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? PoNumber { get; set; }
    public DateTime? PoOrderDate { get; set; }
}

public class GrnTraceSummary
{
    public int GrnId { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public string? DeliveryNoteNumber { get; set; }
}

public class BatchTraceSummary
{
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal ActualQuantity { get; set; }
    public DateTime ProductionDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string QualityStatus { get; set; } = string.Empty;
    public string AssignedCook { get; set; } = string.Empty;
}

public class FinishedGoodsTraceSummary
{
    public int LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantityRemaining { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }
}

public class ShipmentTraceSummary
{
    public int TransferId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public int DestinationBranchId { get; set; }
    public string DestinationBranchName { get; set; } = string.Empty;
    public decimal QuantityShipped { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? DriverName { get; set; }
    public DateTime? DispatchedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
}

public class IngredientLotTraceSummary
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int? LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public decimal QuantityUsed { get; set; }
    public decimal UnitCost { get; set; }
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }
}

public class RecallSimulationResponse
{
    public string RecallNumber { get; set; } = string.Empty;
    public string TargetLotCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime SimulatedAt { get; set; }
    public int AffectedBatchesCount { get; set; }
    public int AffectedFinishedGoodsLotsCount { get; set; }
    public decimal TotalFinishedGoodsQuantityAtRisk { get; set; }
    public int AffectedBranchesCount { get; set; }
    public List<string> AffectedBranchNames { get; set; } = new();
    public decimal TotalEstimatedLoss { get; set; }
    public bool QuarantineHoldApplied { get; set; }
    public string ActionSummary { get; set; } = string.Empty;
}
