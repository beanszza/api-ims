namespace api_scm.Api.Contracts.Responses;

public class SupplierPerformanceReportDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int CompletedOrders { get; set; }
    public int OverdueOrders { get; set; }
    public double OrderAccuracyRate { get; set; }
    public double AverageLeadTimeDays { get; set; }
    public double RejectionRate { get; set; }
    public string OverallVendorGrade { get; set; } = string.Empty;
}
