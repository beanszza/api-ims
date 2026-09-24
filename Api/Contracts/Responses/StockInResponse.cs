using System;
using System.Collections.Generic;

namespace api_scm.Contracts.Responses;

public class StockInResponse
{
    public int StockInId { get; set; }
    public string StockInNumber { get; set; } = string.Empty;
    public int GrnId { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? CommittedBy { get; set; }
    public DateTime? CommittedAt { get; set; }
    public string? Notes { get; set; }
    public List<StockInLineResponse> Lines { get; set; } = new();
}

public class StockInLineResponse
{
    public int StockInLineId { get; set; }
    public int StockInId { get; set; }
    public int? GrnItemId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int? PurchaseUomId { get; set; }
    public string PurchaseUomName { get; set; } = string.Empty;
    public decimal QuantityToStock { get; set; }
    public decimal CurrentStockBeforeCommit { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public bool CommittedToInventory { get; set; }
    public int? InventoryLotId { get; set; }
    public string? Notes { get; set; }
}
