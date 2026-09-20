namespace api_scm.Contracts.Requests;

public class UpdateLocationRequest
{
    public string LocationName { get; set; } = string.Empty;
    public string LocationType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = string.Empty;
}
