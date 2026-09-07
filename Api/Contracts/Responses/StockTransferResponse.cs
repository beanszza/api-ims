using System;

namespace api_scm.Contracts.Responses;

public class StockTransferResponse
{
    public int TransferId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public int? BranchRequestId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int SourceLocationId { get; set; }
    public string SourceLocationName { get; set; } = string.Empty;
    public int DestLocationId { get; set; }
    public string DestLocationName { get; set; } = string.Empty;
    public decimal TransferQuantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? DriverName { get; set; }
    public string? VehiclePlate { get; set; }
    public DateTime TransferDate { get; set; }
    public DateTime? DispatchedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public string? ReceivedBy { get; set; }
}
