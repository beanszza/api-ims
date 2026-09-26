namespace api_scm.Contracts.Responses;

public class FinishedProductResponse
{
    public int ProductId { get; set; }
    public int ItemId { get; set; }
    public decimal SellingPrice { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}