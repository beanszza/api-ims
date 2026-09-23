using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreateStockInRequest
{
    [Required]
    public int GrnId { get; set; }

    public string? Notes { get; set; }

    public bool SubmitForApproval { get; set; } = false;

    public List<CreateStockInLineRequest> Lines { get; set; } = new();
}

public class CreateStockInLineRequest
{
    public int? GrnItemId { get; set; }

    [Required]
    public int ItemId { get; set; }

    public int? PurchaseUomId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Stock to put in must be greater than zero.")]
    public decimal QuantityToStock { get; set; }

    public decimal CurrentStockBeforeCommit { get; set; }

    public string? LotCode { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? Notes { get; set; }
}

public class ApproveStockInRequest
{
    [Required]
    public string ApproverName { get; set; } = string.Empty;

    public string? Notes { get; set; }
}

public class RejectStockInRequest
{
    [Required]
    public string RejectionReason { get; set; } = string.Empty;

    public string? RejectedBy { get; set; }

    public string? Notes { get; set; }
}
