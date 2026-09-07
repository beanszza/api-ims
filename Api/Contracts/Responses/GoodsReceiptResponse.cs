using System;
using System.Collections.Generic;

namespace api_scm.Contracts.Responses;

public class GoodsReceiptResponse
{
    public int GrnId { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public int PoId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int ReceivingLocationId { get; set; }
    public string ReceivingLocationName { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public string DeliveryNoteNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string ReceivedBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<GoodsReceiptItemResponse> Items { get; set; } = new();
}

public class GoodsReceiptItemResponse
{
    public int GrnItemId { get; set; }
    public int PoItemId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public int PurchaseUomId { get; set; }
    public string PurchaseUomName { get; set; } = string.Empty;
    public int? LotId { get; set; }
    public string? LotCode { get; set; }
    public string? SupplierLotCode { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}
