using System;
using System.Collections.Generic;

namespace api_scm.Contracts.Responses;

public class SupplierScorecardResponse
{
    public int SupplierId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    // Volume Metrics
    public int TotalPurchaseOrders { get; set; }
    public int TotalGoodsReceipts { get; set; }
    public decimal TotalSpend { get; set; }

    // Delivery & Quality Metrics
    public decimal OnTimeDeliveryRate { get; set; } // Percentage e.g. 95.5%
    public decimal QualityAcceptanceRate { get; set; } // Percentage e.g. 98.2%
    public decimal DefectRate { get; set; } // Percentage e.g. 1.8%
    public int TotalNcrsFiled { get; set; }
    public int TotalRtvsDispatched { get; set; }

    // Compliance
    public bool IsFdaCompliant { get; set; }
    public bool IsSanitaryCompliant { get; set; }
    public bool IsFullyCompliant { get; set; }

    // Aggregate Rating
    public decimal CompositeScore { get; set; } // Out of 100
    public string PerformanceGrade { get; set; } = "Grade A"; // Grade A, B, C, D
    public string EvaluationSummary { get; set; } = string.Empty;
}
