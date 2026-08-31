using Domains.Entities;
using Domains.Enums;

namespace Applications.Interfaces;

/// <summary>Describes a lot to create when receiving goods.</summary>
/// <param name="ItemId">Item being received.</param>
/// <param name="LocationId">Where it is being received.</param>
/// <param name="Quantity">Quantity, in the item's stocking unit.</param>
/// <param name="SourceType">Where the stock came from.</param>
/// <param name="Status">
/// Starting status. Purchased goods normally arrive as <see cref="LotStatus.Quarantine"/> and are
/// released by inspection.
/// </param>
public sealed record ReceiveLotRequest(
    int ItemId,
    int LocationId,
    decimal Quantity,
    LotSourceType SourceType,
    LotStatus Status = LotStatus.Quarantine)
{
    public int? SupplierId { get; init; }
    public string? SupplierLotNo { get; init; }
    public int? GrnLineId { get; init; }
    public int? ProductionOrderId { get; init; }
    public DateOnly? ManufactureDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public bool IsExpiryEstimated { get; init; }
    public decimal UnitCost { get; init; }

    /// <summary>Overrides the generated lot code. Used when a code was allocated earlier in the flow.</summary>
    public string? LotCode { get; init; }

    public string ReferenceType { get; init; } = string.Empty;
    public string ReferenceId { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

/// <summary>How much to take from one specific lot.</summary>
public sealed record LotDraw(int LotId, decimal Quantity);

/// <summary>
/// The only component permitted to change a lot's quantity.
/// </summary>
/// <remarks>
/// A deliberate choke point. Previously five different services each adjusted stock their own way, with
/// their own idea of what to log, which is why the ledger could not be reconciled against balances.
/// Everything now goes through here, so every movement is written the same way, is always accompanied by
/// a ledger row, and is always attributed.
/// <para>
/// All methods stage changes on the DbContext. They do not save. The caller wraps them in
/// <see cref="IPostingTransaction"/>, which is what makes a multi-lot operation atomic.
/// </para>
/// </remarks>
public interface IStockPostingService
{
    /// <summary>Creates a lot and records its arrival.</summary>
    Task<InventoryLot> ReceiveAsync(ReceiveLotRequest request);

    /// <summary>
    /// Draws stock from specific lots, for example into a production batch.
    /// </summary>
    /// <exception cref="Domains.Exceptions.InsufficientStockException">
    /// A lot does not hold the requested quantity, or is not available.
    /// </exception>
    Task ConsumeAsync(
        IReadOnlyCollection<LotDraw> draws,
        MovementType movementType,
        string referenceType,
        string referenceId,
        string? notes = null);

    /// <summary>Debits lots at a source location as stock leaves it.</summary>
    Task TransferOutAsync(
        IReadOnlyCollection<LotDraw> draws, string referenceType, string referenceId);

    /// <summary>
    /// Credits stock at a destination, preserving the originating lot's identity, expiry and cost.
    /// </summary>
    /// <remarks>
    /// Identity has to survive the move, otherwise the last mile of a recall is severed: knowing a branch
    /// received 20 jars is useless if you cannot tell which batch they were.
    /// </remarks>
    Task<InventoryLot> TransferInAsync(
        int sourceLotId, int destinationLocationId, decimal quantity,
        string referenceType, string referenceId);

    /// <summary>Corrects a lot's quantity to a counted figure. Requires a reason.</summary>
    Task AdjustAsync(int lotId, decimal newQuantity, string reason, string referenceId);

    /// <summary>Writes stock off, moving it to the disposal location and costing the loss.</summary>
    Task DisposeAsync(
        IReadOnlyCollection<LotDraw> draws, string reason, string referenceType, string referenceId);

    /// <summary>
    /// Reverses an earlier movement by posting its mirror image, linked to the original.
    /// </summary>
    Task ReverseAsync(long ledgerId, string reason);

    /// <summary>
    /// Recomputes a lot's remaining quantity from its ledger rows and reports any disagreement.
    /// </summary>
    /// <remarks>
    /// The reconciliation check. If this ever returns a non-zero difference, either something bypassed
    /// this service or a ledger row was tampered with; both are worth knowing about loudly.
    /// </remarks>
    Task<decimal> ReconcileLotAsync(int lotId);
}
