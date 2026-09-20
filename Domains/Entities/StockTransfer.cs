using System;
using Domains.Enums;

namespace Domains.Entities;

public class StockTransfer
{
    public int TransferId { get; set; }

    /// <summary>Canonical human-readable shipment number, e.g. "SH-2026-0001".</summary>
    public string TransferNumber { get; set; } = string.Empty;

    public int? BranchRequestId { get; set; }
    public BranchRequest? BranchRequest { get; set; }

    public int ProductId { get; set; }
    public FinishedProduct? Product { get; set; }

    public int SourceLocationId { get; set; }
    public Location? SourceLocation { get; set; }

    public int DestLocationId { get; set; }
    public Location? DestLocation { get; set; }

    public decimal TransferQuantity { get; set; }
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;

    public string? DriverName { get; set; }
    public string? VehiclePlate { get; set; }

    public DateTime TransferDate { get; set; } = DateTime.UtcNow;
    public DateTime? DispatchedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public string? ReceivedBy { get; set; }
}