namespace api_scm.Contracts.Requests;

public class UpdateSupplierRequest
{
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public bool? IsActive { get; set; }
    public List<int>? SuppliedItemIds { get; set; }
}
