using Domains.Enums;

namespace Domains.Entities;

/// <summary>
/// A distinct, identifiable quantity of one item at one location.
/// </summary>
/// <remarks>
/// The unit of stock the whole rebuild turns on. Before this existed, on-hand stock was a single
/// integer per (item, location) - a number with no memory. That one fact made every one of the
/// following unrepresentable:
/// <list type="bullet">
///   <item>"9 kg came from Supplier A and 100 kg from Supplier B" - two lots, trivially.</item>
///   <item>"which supplier's ube went into this jar" - via the consumption rows that reference lots.</item>
///   <item>"what expires next" - <see cref="ExpiryDate"/> per lot.</item>
///   <item>"consume oldest first" - <see cref="ReceivedDate"/> per lot.</item>
///   <item>"80 kg accepted, 10 kg rejected" - two lots with different statuses.</item>
///   <item>"what did this cost" - <see cref="UnitCost"/> per lot.</item>
/// </list>
/// <para>
/// From Task 10 <c>Inventory.CurrentStock</c> becomes a cached projection of the available lots here,
/// and this becomes the source of truth.
/// </para>
/// </remarks>
public class InventoryLot
{
    public int LotId { get; set; }

    /// <summary>
    /// Human-readable, unique identifier, for example <c>L-260619-UBE-01</c>.
    /// </summary>
    /// <remarks>
    /// For produced stock this is the traceability lot code: the thing printed on the jar and the thing a
    /// recall searches on. Kept short and typeable for that reason.
    /// </remarks>
    public string LotCode { get; set; } = string.Empty;

    public int ItemId { get; set; }

    /// <summary>Where this lot physically is. A lot moves by being split, never by being reassigned.</summary>
    public int LocationId { get; set; }

    public LotSourceType SourceType { get; set; }

    /// <summary>Supplier this lot came from. The anchor for bad-supplier attribution.</summary>
    public int? SupplierId { get; set; }

    /// <summary>
    /// The supplier's own lot or batch number, as printed on their delivery.
    /// </summary>
    /// <remarks>
    /// Recorded separately from our own code because when a supplier issues a recall they quote their
    /// number, not ours. Without this, matching their notice to our stock is manual.
    /// </remarks>
    public string? SupplierLotNo { get; set; }

    /// <summary>Receipt line that created this lot, giving the path back to the purchase order.</summary>
    public int? GrnLineId { get; set; }

    /// <summary>Production order that created this lot, for produced stock.</summary>
    public int? ProductionOrderId { get; set; }

    /// <summary>When the lot entered stock. Drives FIFO.</summary>
    public DateTime ReceivedDate { get; set; }

    /// <summary>When the goods were made, per the supplier's label or our own production date.</summary>
    /// <remarks>
    /// A <see cref="DateOnly"/>, not a timestamp. A manufacture date is what is printed on a label; it has
    /// no time of day and no timezone, and storing it as an instant invites the date to shift when read in
    /// a different zone.
    /// </remarks>
    public DateOnly? ManufactureDate { get; set; }

    /// <summary>
    /// When the lot expires. Drives FEFO and the expiry dashboard. Null for items that do not perish.
    /// </summary>
    /// <remarks>
    /// Also a <see cref="DateOnly"/>. "Best before 25 June" is a calendar date, and a lot must not become
    /// expired an hour earlier for one user than another.
    /// </remarks>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>
    /// True when <see cref="ExpiryDate"/> was calculated from shelf life rather than read from a label.
    /// </summary>
    /// <remarks>
    /// Worth distinguishing: a calculated date is an assumption, and during an investigation the
    /// difference between "the supplier said this" and "we assumed this" matters.
    /// </remarks>
    public bool IsExpiryEstimated { get; set; }

    /// <summary>Quantity originally in the lot, in the item's stocking unit.</summary>
    public decimal QuantityReceived { get; set; }

    /// <summary>
    /// Quantity still in the lot. Only counts toward available stock when
    /// <see cref="Status"/> is <see cref="LotStatus.Available"/>.
    /// </summary>
    public decimal QuantityRemaining { get; set; }

    /// <summary>The unit both quantities are expressed in: always the item's stocking unit.</summary>
    public int UomId { get; set; }

    /// <summary>Cost per stocking unit, used for valuation and for costing what waste actually cost.</summary>
    public decimal UnitCost { get; set; }

    public LotStatus Status { get; set; } = LotStatus.Quarantine;

    /// <summary>Why the lot is on hold, when it is. Required context for a recall investigation.</summary>
    public string? HoldReason { get; set; }

    /// <summary>True for lots created by the conversion of legacy balances, so they can be told apart.</summary>
    public bool IsOpeningBalance { get; set; }

    public Item? Item { get; set; }
    public Location? Location { get; set; }
    public Supplier? Supplier { get; set; }
    public UnitOfMeasure? Uom { get; set; }

    /// <summary>Quantity available to consume or ship right now.</summary>
    public decimal AvailableQuantity =>
        Status == LotStatus.Available ? QuantityRemaining : 0m;

    /// <summary>
    /// Whole days until expiry: 0 means it expires today, negative means it already has.
    /// Null when the lot does not expire.
    /// </summary>
    public int? DaysUntilExpiry(DateOnly asOf) =>
        ExpiryDate is null ? null : ExpiryDate.Value.DayNumber - asOf.DayNumber;

    /// <summary>True when the lot's expiry date has passed.</summary>
    public bool IsExpiredAsOf(DateOnly asOf) => ExpiryDate is not null && ExpiryDate.Value < asOf;
}
