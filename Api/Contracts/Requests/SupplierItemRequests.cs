using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreateOrUpdateSupplierItemRequest
{
    [Required]
    public int SupplierId { get; set; }

    [Required]
    public int ItemId { get; set; }

    public string? SupplierSku { get; set; }
    public string? SupplierItemName { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Unit price must be non-negative.")]
    public decimal UnitPrice { get; set; }

    public string Currency { get; set; } = "PHP";

    public int PurchaseUomId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Pack size must be greater than zero.")]
    public decimal PackSize { get; set; } = 1m;

    [Range(0, 365, ErrorMessage = "Lead time days must be valid.")]
    public int LeadTimeDays { get; set; } = 3;

    [Range(0.001, double.MaxValue, ErrorMessage = "Minimum order quantity must be greater than zero.")]
    public decimal MinOrderQuantity { get; set; } = 1m;

    public bool IsPreferred { get; set; }

    public bool IsActive { get; set; } = true;
}
