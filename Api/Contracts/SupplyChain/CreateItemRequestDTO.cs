namespace api_scm.Contracts.Requests;

public class CreateItemRequest
{
    public int UomId { get; set; }
    public int CategoryId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int MinStockLevel { get; set; }
    public int MaxStockLevel { get; set; }
    public bool IsActive { get; set; } = true;
}