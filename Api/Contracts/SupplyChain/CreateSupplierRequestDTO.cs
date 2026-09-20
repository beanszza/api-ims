namespace api_scm.Contracts.Requests;

public class CreateSupplierRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Website { get; set; }
    public bool IsActive { get; set; } = true;
    public List<int> SuppliedItemIds { get; set; } = new();
}