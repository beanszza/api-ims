namespace api_scm.Contracts.Requests;

public class UpdateItemRequest
{
    public int? UomId { get; set; }
    public int? CategoryId { get; set; }
    public string? ItemName { get; set; }
    public int? MinStockLevel { get; set; }
    public int? MaxStockLevel { get; set; }
    public bool? IsActive { get; set; }
}