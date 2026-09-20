namespace api_scm.Contracts.Requests;

public class UpdateItemRequest
{
    public int? UomId { get; set; }
    public int? CategoryId { get; set; }
    public string? ItemName { get; set; }
    public decimal? MinStockLevel { get; set; }
    public decimal? MaxStockLevel { get; set; }
    public bool? IsActive { get; set; }
}