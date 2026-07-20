using System.Collections.Generic;

namespace api_scm.Api.Contracts.Responses;

public class ProcurementReportResponseDto
{
    public IEnumerable<HistoricalProcurementAuditDto> HistoricalAudit { get; set; } = new List<HistoricalProcurementAuditDto>();
    public IEnumerable<SmartProcurementAdviceDto> ProcurementAdvice { get; set; } = new List<SmartProcurementAdviceDto>();
}

public class HistoricalProcurementAuditDto
{
    public string PoId { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string TotalItemsCount { get; set; } = string.Empty;
    public string TotalOrderedQty { get; set; } = string.Empty;
    public string DeliveryLeadTime { get; set; } = string.Empty;
    public string FulfillmentRate { get; set; } = string.Empty;
    public string InspectionStatus { get; set; } = string.Empty;
}

public class SmartProcurementAdviceDto
{
    public string ItemName { get; set; } = string.Empty;
    public string OrderTrend { get; set; } = string.Empty;
    public string PredictedNextMonthQty { get; set; } = string.Empty;
    public string AiConfidence { get; set; } = string.Empty;
    public string RecommendationBasis { get; set; } = string.Empty;
}
