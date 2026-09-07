using System;
using System.Collections.Generic;

namespace api_scm.Contracts.Responses;

public class BranchRequestResponse
{
    public int BranchRequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string? Notes { get; set; }
    public List<BranchRequestItemResponse> Items { get; set; } = new();
}

public class BranchRequestItemResponse
{
    public int BranchRequestItemId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal ApprovedQuantity { get; set; }
    public decimal DispatchedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public string? Notes { get; set; }
}

public class BranchReturnResponse
{
    public int BranchReturnId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ReturnedBy { get; set; } = string.Empty;
    public string? AuthorizedBy { get; set; }
    public string? Notes { get; set; }
    public List<BranchReturnItemResponse> Items { get; set; } = new();
}

public class BranchReturnItemResponse
{
    public int BranchReturnItemId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int? LotId { get; set; }
    public string? LotCode { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public string? DefectCondition { get; set; }
    public string? Notes { get; set; }
}
