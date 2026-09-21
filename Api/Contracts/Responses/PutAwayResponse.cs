using System;

namespace api_scm.Contracts.Responses;

public class PutAwayResponse
{
    public int PutAwayId { get; set; }
    public string PutAwayNumber { get; set; } = string.Empty;
    public int GrnId { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public int? PoId { get; set; }
    public string? PoNumber { get; set; }
    public int? PrId { get; set; }
    public string? PrNumber { get; set; }
    public string? SupplierName { get; set; }
    public int GrnItemId { get; set; }
    public int? QaInspectionId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal AcceptedQuantity { get; set; }
    public int UomId { get; set; }
    public string UomName { get; set; } = string.Empty;
    public int? DestinationLocationId { get; set; }
    public string? DestinationLocationName { get; set; }
    public int? LotId { get; set; }
    public string? LotCode { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? SerialNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PerformedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public bool IsLotTracked { get; set; }
    public bool IsExpiryTracked { get; set; }
}
