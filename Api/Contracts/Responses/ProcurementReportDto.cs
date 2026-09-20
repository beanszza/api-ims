using System.Collections.Generic;

namespace api_scm.Api.Contracts.Responses;

public class ProcurementReportResponseDto
{
    public OrderFulfillmentSummaryDto OrderFulfillmentSummary { get; set; } = new OrderFulfillmentSummaryDto();
    public IEnumerable<HistoricalProcurementAuditDto> HistoricalAudit { get; set; } = new List<HistoricalProcurementAuditDto>();
}

public class OrderFulfillmentSummaryDto
{
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int ArrivedOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int RejectedOrders { get; set; }
    public int CancelledOrders { get; set; }
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
