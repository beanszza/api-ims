namespace api_scm.Contracts.Requests;

public class CreateFinishedProductRequest
{
    public int ItemId { get; set; }
    public decimal SellingPrice { get; set; }
    public string Sku { get; set; } = string.Empty;
}