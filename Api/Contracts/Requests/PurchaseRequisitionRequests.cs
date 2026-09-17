using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreatePurchaseRequisitionRequest
{
    public string? RequestedBy { get; set; }

    [Required]
    public string Department { get; set; } = string.Empty;

    public DateTime RequiredDate { get; set; }

    public string? RequestType { get; set; }

    public string? Priority { get; set; }

    public string? Purpose { get; set; }

    public string? Notes { get; set; }

    public bool SubmitForApproval { get; set; }

    [Required]
    public List<CreatePurchaseRequisitionItemRequest> Items { get; set; } = new();
}

public class UpdatePurchaseRequisitionRequest
{
    public string? RequestedBy { get; set; }

    [Required]
    public string Department { get; set; } = string.Empty;

    public DateTime RequiredDate { get; set; }

    public string? RequestType { get; set; }

    public string? Priority { get; set; }

    public string? Purpose { get; set; }

    public string? Notes { get; set; }

    public bool SubmitForApproval { get; set; }

    [Required]
    public List<CreatePurchaseRequisitionItemRequest> Items { get; set; } = new();
}

public class CreatePurchaseRequisitionItemRequest
{
    [Required]
    public int ItemId { get; set; }

    public int? SuggestedSupplierId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Requested quantity must be positive.")]
    public decimal RequestedQuantity { get; set; }

    public int? PurchaseUomId { get; set; }

    public decimal? EstimatedUnitPrice { get; set; }
}

public class UpdatePurchaseRequisitionStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;

    public string? Comments { get; set; }

    public string? AdminNotes { get; set; }
}

