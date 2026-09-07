using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreateGoodsReceiptRequest
{
    [Required]
    public int PoId { get; set; }

    public int? ReceivingLocationId { get; set; }

    [Required]
    public string DeliveryNoteNumber { get; set; } = string.Empty;

    public string? Carrier { get; set; }

    public string? Notes { get; set; }

    [Required]
    public List<CreateGoodsReceiptItemRequest> Items { get; set; } = new();
}

public class CreateGoodsReceiptItemRequest
{
    [Required]
    public int PoItemId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Delivered quantity must be positive.")]
    public decimal DeliveredQuantity { get; set; }

    public string? SupplierLotCode { get; set; }

    public DateOnly? ManufactureDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }
}
