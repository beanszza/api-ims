using System;
using System.Collections.Generic;

namespace api_scm.Contracts.Responses;

public class DisposalRecordResponse
{
    public int DisposalId { get; set; }
    public string DisposalNumber { get; set; } = string.Empty;
    public DateTime DisposalDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal TotalCost { get; set; }
    public string AuthorizedBy { get; set; } = string.Empty;
    public string? WitnessName { get; set; }
    public string? Notes { get; set; }
    public List<DisposalRecordItemResponse> Items { get; set; } = new();
}

public class DisposalRecordItemResponse
{
    public int DisposalItemId { get; set; }
    public int LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityDisposed { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal => QuantityDisposed * UnitCost;
    public string? Notes { get; set; }
}

public class ExpirySweepResponse
{
    public DateTime SweptAt { get; set; }
    public int ExpiredLotsCount { get; set; }
    public decimal TotalQuantityQuarantined { get; set; }
    public decimal TotalEstimatedLoss { get; set; }
    public List<ExpiredLotDetail> ExpiredLots { get; set; } = new();
}

public class ExpiredLotDetail
{
    public int LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }
    public decimal QuantityExpired { get; set; }
    public decimal UnitCost { get; set; }
}
