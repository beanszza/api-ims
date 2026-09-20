using Domains.Enums;

namespace api_scm.Contracts.Responses;

public class LotResponse
{
    public int LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierLotNo { get; set; }
    public DateTime ReceivedDate { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal QuantityRemaining { get; set; }
    public string UomName { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOpeningBalance { get; set; }

    /// <summary>
    /// QuantityRemaining as a percentage of total available stock for this item at this location.
    /// Only populated by GetLotsByItemAsync; null on the paginated flat list.
    /// </summary>
    public decimal? SharePercent { get; set; }

    /// <summary>
    /// True for the earliest-expiry Available lot — the lot FEFO says to consume first.
    /// Only populated by GetLotsByItemAsync.
    /// </summary>
    public bool IsFefoNext { get; set; }

    // Computed client-side convenience flags
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Days until expiry. Negative means already expired.</summary>
    public int? DaysUntilExpiry => ExpiryDate.HasValue
        ? ExpiryDate.Value.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber
        : null;
}