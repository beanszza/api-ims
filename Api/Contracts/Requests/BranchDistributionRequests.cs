using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace api_scm.Contracts.Requests;

public class CreateBranchRequestDto
{
    [Required]
    public int BranchId { get; set; }

    public DateTime? RequiredDate { get; set; }

    public string? Notes { get; set; }

    [Required]
    public List<CreateBranchRequestItemDto> Items { get; set; } = new();
}

public class CreateBranchRequestItemDto
{
    [Required]
    public int ProductId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Requested quantity must be positive.")]
    public decimal RequestedQuantity { get; set; }

    public string? Notes { get; set; }
}

public class DispatchShipmentRequest
{
    public string? DriverName { get; set; }
    public string? VehiclePlate { get; set; }
}

public class ReceiveShipmentRequest
{
    public string? ReceivedBy { get; set; }
    public string? Notes { get; set; }
}

public class CreateBranchReturnRequest
{
    [Required]
    public int BranchId { get; set; }

    [Required]
    public string Reason { get; set; } = "Damaged in Store"; // Near Expiry, Damaged in Store, Customer Return

    public string? Notes { get; set; }

    [Required]
    public List<CreateBranchReturnItemDto> Items { get; set; } = new();
}

public class CreateBranchReturnItemDto
{
    [Required]
    public int ProductId { get; set; }

    public int? LotId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Returned quantity must be positive.")]
    public decimal ReturnedQuantity { get; set; }

    public string? DefectCondition { get; set; }
    public string? Notes { get; set; }
}
