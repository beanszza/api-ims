namespace api_scm.Contracts.Responses;

public class SupplierResponse
{
    public int SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Website { get; set; }
    public bool IsActive { get; set; }
    public List<SupplierSupplyItemResponse> SuppliedItems { get; set; } = new();
}

public class SupplierSupplyItemResponse
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
}