namespace api_scm.Api.Contracts.Responses;

public class DistributionReportDto
{
    public int TransferId { get; set; }
    public string SourceLocation { get; set; } = string.Empty;
    public string DestLocation { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public double TransitDurationHours { get; set; }
    public string Status { get; set; } = string.Empty;
    public System.DateTime TransferDate { get; set; }
}
