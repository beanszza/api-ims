namespace api_scm.Api.Contracts.Responses;

public class ProcurementReportDto
{
    public int PoId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double FulfillmentRate { get; set; }
    public double LeadTimeDays { get; set; }
    public System.DateTime OrderDate { get; set; }
}
