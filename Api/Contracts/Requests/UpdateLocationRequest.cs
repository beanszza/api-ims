namespace api_scm.Contracts.Requests;

public class UpdateLocationRequest
{
    public string LocationName { get; set; } = string.Empty;
    public string LocationType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
