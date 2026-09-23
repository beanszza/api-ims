using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreateGoodsReceiptRequest
{
    /// <summary>Mandatory arrived shipment that is the sole source of this GRN.</summary>
    [Required]
    public int DeliveryId { get; set; }

    public string? DeliveryNoteNumber { get; set; }

    public string? SupplierDrNumber { get; set; }

    public string? SupplierInvoiceNumber { get; set; }

    public string? ReceivingBay { get; set; }

    public string? Carrier { get; set; }

    public string? Notes { get; set; }

    public bool PhysicalQuantityVerified { get; set; }
    public bool ItemsMatchPurchaseOrder { get; set; }
    public bool SupplierDocumentsChecked { get; set; }
    public bool PackagingConditionChecked { get; set; }

    [Required]
    public List<CreateGoodsReceiptItemRequest> Items { get; set; } = new();
}

public class UpdateGoodsReceiptRequest
{
    public string? DeliveryNoteNumber { get; set; }

    public string? SupplierDrNumber { get; set; }

    public string? SupplierInvoiceNumber { get; set; }

    public string? ReceivingBay { get; set; }

    public string? Carrier { get; set; }

    public string? Notes { get; set; }

    public List<CreateGoodsReceiptItemRequest> Items { get; set; } = new();
}

public class CreateGoodsReceiptItemRequest
{
    [Required]
    public int PoItemId { get; set; }

    public int? ItemId { get; set; }

    public int? DeliveryItemId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Actual received quantity must be non-negative.")]
    public decimal DeliveredQuantity { get; set; }

    public string? SupplierLotCode { get; set; }

    public DateOnly? ManufactureDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string? Notes { get; set; }
}

public class RejectGrnRequest
{
    [Required]
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
