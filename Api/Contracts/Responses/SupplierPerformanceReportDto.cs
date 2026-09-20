using System.Collections.Generic;

namespace api_scm.Api.Contracts.Responses;

public class SupplierPerformanceReportResponseDto
{
    public IEnumerable<VendorScorecardAuditDto> VendorScorecard { get; set; } = new List<VendorScorecardAuditDto>();
}

public class VendorScorecardAuditDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string TotalOrdersPlaced { get; set; } = string.Empty;
    public string OnTimeDeliveries { get; set; } = string.Empty;
    public string LateDeliveries { get; set; } = string.Empty;
    public string OrderAccuracyRate { get; set; } = string.Empty;
    public string AverageLeadTime { get; set; } = string.Empty;
    public string RejectionRate { get; set; } = string.Empty;
    public string OverallVendorGrade { get; set; } = string.Empty;
    public List<SupplierOrderTransactionDto> Orders { get; set; } = new List<SupplierOrderTransactionDto>();
}

public class SupplierOrderTransactionDto
{
    public int PoId { get; set; }
    public string PoCode { get; set; } = string.Empty;
    public string OrderDate { get; set; } = string.Empty;
    public string ExpectedArrivalDate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalItemsCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string QaStatus { get; set; } = string.Empty;
    public string InspectedDate { get; set; } = string.Empty;
}
