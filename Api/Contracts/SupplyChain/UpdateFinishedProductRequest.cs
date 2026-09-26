namespace api_scm.Contracts.Requests;

public class UpdateFinishedProductRequest
{
    public string ProductName { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}
