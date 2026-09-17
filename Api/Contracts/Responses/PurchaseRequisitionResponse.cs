using System;
using System.Collections.Generic;

namespace api_scm.Contracts.Responses;

public class PurchaseRequisitionResponse
{
    public int PrId { get; set; }
    public string PrNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public DateTime RequiredDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RequestType { get; set; }
    public string? Priority { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
    public string? AdminNotes { get; set; }
    public decimal EstimatedTotalAmount { get; set; }
    public string? GeneratedPoNumbers { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<PurchaseRequisitionItemResponse> Items { get; set; } = new();
}

public class PurchaseRequisitionItemResponse
{
    public int PrItemId { get; set; }
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int? SuggestedSupplierId { get; set; }
    public string? SuggestedSupplierName { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal ActualInventory { get; set; }
    public int PurchaseUomId { get; set; }
    public string PurchaseUomName { get; set; } = string.Empty;
    public decimal EstimatedUnitPrice { get; set; }
    public decimal EstimatedLineTotal => RequestedQuantity * EstimatedUnitPrice;
}

public class PrFanOutResultResponse
{
    public int PrId { get; set; }
    public string PrNumber { get; set; } = string.Empty;
    public int PurchaseOrdersCreatedCount { get; set; }
    public List<PurchaseOrderResponse> GeneratedPurchaseOrders { get; set; } = new();
}
