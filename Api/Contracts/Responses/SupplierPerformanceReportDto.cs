using System.Collections.Generic;

namespace api_scm.Api.Contracts.Responses;

public class SupplierPerformanceReportResponseDto
{
    public IEnumerable<VendorScorecardAuditDto> VendorScorecard { get; set; } = new List<VendorScorecardAuditDto>();
}

public class VendorScorecardAuditDto
{
    public string SupplierName { get; set; } = string.Empty;
    public string TotalOrdersPlaced { get; set; } = string.Empty;
    public string OnTimeDeliveries { get; set; } = string.Empty;
    public string LateDeliveries { get; set; } = string.Empty;
    public string OrderAccuracyRate { get; set; } = string.Empty;
    public string AverageLeadTime { get; set; } = string.Empty;
    public string RejectionRate { get; set; } = string.Empty;
    public string OverallVendorGrade { get; set; } = string.Empty;
}
