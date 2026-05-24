namespace api_scm.Contracts.Requests;

public class UpdateSupplierRequest
{
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool? IsActive { get; set; }
}
