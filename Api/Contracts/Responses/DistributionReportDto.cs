using System.Collections.Generic;

namespace api_scm.Api.Contracts.Responses;

public class DistributionReportResponseDto
{
    public IEnumerable<LogisticsTransferVelocityDto> LogisticsVelocity { get; set; } = new List<LogisticsTransferVelocityDto>();
}

public class LogisticsTransferVelocityDto
{
    public string TransferId { get; set; } = string.Empty;
    public string SourceLocation { get; set; } = string.Empty;
    public string DestinationBranch { get; set; } = string.Empty;
    public string DispatchDate { get; set; } = string.Empty;
    public string ReceiveDate { get; set; } = string.Empty;
    public string TransitDuration { get; set; } = string.Empty;
    public string AssignedDriver { get; set; } = string.Empty;
    public string TransferStatus { get; set; } = string.Empty;
}
