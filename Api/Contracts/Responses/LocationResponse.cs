namespace api_scm.Contracts.Responses;

public class LocationResponse
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string LocationType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
