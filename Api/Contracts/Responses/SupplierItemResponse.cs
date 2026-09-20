using System;

namespace api_scm.Contracts.Responses;

public class SupplierItemResponse
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? SupplierSku { get; set; }
    public string? SupplierItemName { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "PHP";
    public int PurchaseUomId { get; set; }
    public string PurchaseUomName { get; set; } = string.Empty;
    public decimal PackSize { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal MinOrderQuantity { get; set; }
    public bool IsPreferred { get; set; }
    public decimal? LastPurchasePrice { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public bool IsActive { get; set; }
}

public class ItemSupplierOptionResponse
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "PHP";
    public int PurchaseUomId { get; set; }
    public string PurchaseUomName { get; set; } = string.Empty;
    public decimal PackSize { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal MinOrderQuantity { get; set; }
    public bool IsPreferred { get; set; }
}
