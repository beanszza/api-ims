using System;

namespace api_scm.Contracts.Responses;

public class DiscrepancyResponse
{
    public int DiscrepancyId { get; set; }
    public string DiscrepancyNumber { get; set; } = string.Empty;
    public string DiscrepancyType { get; set; } = string.Empty;
    public int GrnId { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public int PoId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public int? PrId { get; set; }
    public string? PrNumber { get; set; }
    public string? SupplierName { get; set; }
    public int? DeliveryId { get; set; }
    public string? DeliveryNumber { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal PreviouslyReceivedQty { get; set; }
    public decimal CurrentReceivedQty { get; set; }
    public decimal DiscrepancyQuantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResolutionType { get; set; }
    public string? ResolutionNotes { get; set; }
    public int? LossReportId { get; set; }
    public string? LossReportNumber { get; set; }
    public int? RtvId { get; set; }
    public string? RtvNumber { get; set; }
    public int? NcrId { get; set; }
    public string? NcrNumber { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LossReportResponse
{
    public int LossReportId { get; set; }
    public string LossReportNumber { get; set; } = string.Empty;
    public int? DiscrepancyId { get; set; }
    public string? DiscrepancyNumber { get; set; }
    public int? GrnId { get; set; }
    public string? GrnNumber { get; set; }
    public int? PoId { get; set; }
    public string? PoNumber { get; set; }
    public int? PrId { get; set; }
    public string? PrNumber { get; set; }
    public string? SupplierName { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal LostQuantity { get; set; }
    public int UomId { get; set; }
    public string UomName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string AuthorisedBy { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long? StockLedgerEntryId { get; set; }
}
