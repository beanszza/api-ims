using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Domains.Exceptions;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Applications.Services;

/// <inheritdoc />
public sealed class StockPostingService : IStockPostingService
{
    private readonly ScmDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILotCodeGenerator _lotCodes;
    private readonly ILocationResolver _locations;
    private readonly IStatusTransitionGuard _statusGuard;

    public StockPostingService(
        ScmDbContext context,
        ICurrentUserService currentUser,
        ILotCodeGenerator lotCodes,
        ILocationResolver locations,
        IStatusTransitionGuard statusGuard)
    {
        _context = context;
        _currentUser = currentUser;
        _lotCodes = lotCodes;
        _locations = locations;
        _statusGuard = statusGuard;
    }

    public async Task<InventoryLot> ReceiveAsync(ReceiveLotRequest request)
    {
        if (request.Quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request), request.Quantity, "A receipt must be for a positive quantity.");
        }

        var item = await _context.Items
            .FirstOrDefaultAsync(i => i.ItemId == request.ItemId)
            ?? throw new InvalidOperationException($"Item {request.ItemId} was not found.");

        var receivedAt = DateTime.UtcNow;

        var lot = new InventoryLot
        {
            LotCode = request.LotCode
                      ?? await _lotCodes.ForPurchasedLotAsync(request.ItemId, receivedAt),
            ItemId = request.ItemId,
            LocationId = request.LocationId,
            SourceType = request.SourceType,
            SupplierId = request.SupplierId,
            SupplierLotNo = request.SupplierLotNo,
            GrnLineId = request.GrnLineId,
            ProductionOrderId = request.ProductionOrderId,
            ReceivedDate = receivedAt,
            ManufactureDate = request.ManufactureDate,
            ExpiryDate = request.ExpiryDate,
            IsExpiryEstimated = request.IsExpiryEstimated,
            QuantityReceived = request.Quantity,
            QuantityRemaining = request.Quantity,
            // Always the item's stocking unit: callers convert before they get here, so a lot's two
            // quantities and its ledger rows are guaranteed to share one unit.
            UomId = item.StockUomId,
            UnitCost = request.UnitCost,
            Status = request.Status,
            IsOpeningBalance = request.SourceType == LotSourceType.OpeningBalance
        };

        _context.InventoryLots.Add(lot);

        var movementType = request.SourceType switch
        {
            LotSourceType.Purchased => MovementType.PurchaseReceipt,
            LotSourceType.Produced => MovementType.ProductionOutput,
            LotSourceType.OpeningBalance => MovementType.OpeningBalance,
            LotSourceType.BranchReturn => MovementType.BranchReturn,
            _ => MovementType.Adjustment
        };

        AddLedgerEntry(lot, movementType, request.Quantity,
            request.ReferenceType, request.ReferenceId, request.Notes);

        // A brand new lot has no "before": its available contribution starts at zero.
        await AdjustInventoryCacheAsync(lot.ItemId, lot.LocationId, lot.AvailableQuantity);

        return lot;
    }

    public async Task ConsumeAsync(
        IReadOnlyCollection<LotDraw> draws,
        MovementType movementType,
        string referenceType,
        string referenceId,
        string? notes = null)
    {
        foreach (var draw in draws)
        {
            var lot = await LoadLotAsync(draw.LotId);

            if (lot.Status != LotStatus.Available)
            {
                throw new InsufficientStockException(
                    $"Lot {lot.LotCode} is {EnumDbValue.ToDbValue(lot.Status)} and cannot be consumed.");
            }

            var before = lot.AvailableQuantity;
            Withdraw(lot, draw.Quantity);
            await AdjustInventoryCacheAsync(lot.ItemId, lot.LocationId, lot.AvailableQuantity - before);

            AddLedgerEntry(lot, movementType, -draw.Quantity, referenceType, referenceId, notes);
        }
    }

    public async Task TransferOutAsync(
        IReadOnlyCollection<LotDraw> draws, string referenceType, string referenceId)
    {
        foreach (var draw in draws)
        {
            var lot = await LoadLotAsync(draw.LotId);

            if (lot.Status != LotStatus.Available)
            {
                throw new InsufficientStockException(
                    $"Lot {lot.LotCode} is {EnumDbValue.ToDbValue(lot.Status)} and cannot be dispatched.");
            }

            var before = lot.AvailableQuantity;
            Withdraw(lot, draw.Quantity);
            await AdjustInventoryCacheAsync(lot.ItemId, lot.LocationId, lot.AvailableQuantity - before);

            AddLedgerEntry(lot, MovementType.TransferOut, -draw.Quantity, referenceType, referenceId);
        }
    }

    public async Task<InventoryLot> TransferInAsync(
        int sourceLotId, int destinationLocationId, decimal quantity,
        string referenceType, string referenceId)
    {
        var source = await LoadLotAsync(sourceLotId);

        // A new row at the destination, but the same lot identity, expiry and cost. The lot code is
        // deliberately reused: it is the thing a recall searches on, and renaming it on arrival would
        // break the chain between what left and what landed.
        var arriving = new InventoryLot
        {
            LotCode = source.LotCode,
            ItemId = source.ItemId,
            LocationId = destinationLocationId,
            SourceType = source.SourceType,
            SupplierId = source.SupplierId,
            SupplierLotNo = source.SupplierLotNo,
            GrnLineId = source.GrnLineId,
            ProductionOrderId = source.ProductionOrderId,
            ReceivedDate = source.ReceivedDate,
            ManufactureDate = source.ManufactureDate,
            ExpiryDate = source.ExpiryDate,
            IsExpiryEstimated = source.IsExpiryEstimated,
            QuantityReceived = quantity,
            QuantityRemaining = quantity,
            UomId = source.UomId,
            UnitCost = source.UnitCost,
            Status = source.Status,
            IsOpeningBalance = source.IsOpeningBalance
        };

        _context.InventoryLots.Add(arriving);
        AddLedgerEntry(arriving, MovementType.TransferIn, quantity, referenceType, referenceId);

        // A brand new lot at the destination: its available contribution starts at zero, so the whole
        // of AvailableQuantity is the delta (zero unless the arriving status is itself Available).
        await AdjustInventoryCacheAsync(arriving.ItemId, arriving.LocationId, arriving.AvailableQuantity);

        return arriving;
    }

    public async Task AdjustAsync(int lotId, decimal newQuantity, string reason, string referenceId)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "An adjustment needs a reason: an unexplained stock change is indistinguishable from an error.",
                nameof(reason));
        }

        var lot = await LoadLotAsync(lotId);
        var delta = newQuantity - lot.QuantityRemaining;

        if (delta == 0)
        {
            return;
        }

        if (newQuantity < 0)
        {
            throw new InsufficientStockException(
                $"Lot {lot.LotCode} cannot be adjusted to a negative quantity ({newQuantity}).");
        }

        // Counting more than was received means the received figure was wrong, so raise it too rather
        // than violating the remaining-within-received invariant.
        if (newQuantity > lot.QuantityReceived)
        {
            lot.QuantityReceived = newQuantity;
        }

        var before = lot.AvailableQuantity;
        lot.QuantityRemaining = newQuantity;
        SettleStatus(lot);
        await AdjustInventoryCacheAsync(lot.ItemId, lot.LocationId, lot.AvailableQuantity - before);

        AddLedgerEntry(lot, MovementType.Adjustment, delta, "StockAdjustment", referenceId, reason);
    }

    public async Task DisposeAsync(
        IReadOnlyCollection<LotDraw> draws, string reason, string referenceType, string referenceId)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A disposal needs a reason code.", nameof(reason));
        }

        // Resolved rather than assumed: written-off stock still has to live somewhere so the write-off
        // remains auditable instead of the quantity simply vanishing.
        _ = await _locations.RequireSystemLocationAsync(LocationType.Disposal);

        foreach (var draw in draws)
        {
            var lot = await LoadLotAsync(draw.LotId);
            var before = lot.AvailableQuantity;

            // Withdraw without the generic "fully drawn -> Consumed" side effect: disposal has its own
            // terminal status, and letting SettleStatus land the lot on Consumed first would make the
            // guard reject the Consumed -> Disposed transition immediately after.
            Withdraw(lot, draw.Quantity, settleStatus: false);
            AddLedgerEntry(lot, MovementType.Disposal, -draw.Quantity, referenceType, referenceId, reason);

            if (lot.QuantityRemaining == 0)
            {
                TrySetStatus(lot, LotStatus.Disposed);
            }

            await AdjustInventoryCacheAsync(lot.ItemId, lot.LocationId, lot.AvailableQuantity - before);
        }
    }

    public async Task ReverseAsync(long ledgerId, string reason)
    {
        var original = await _context.StockLedgers
            .FirstOrDefaultAsync(l => l.LedgerId == ledgerId)
            ?? throw new InvalidOperationException($"Ledger entry {ledgerId} was not found.");

        if (await _context.StockLedgers.AnyAsync(l => l.ReversalOfLedgerId == ledgerId))
        {
            throw new InvalidOperationException(
                $"Ledger entry {ledgerId} has already been reversed.");
        }

        var lot = await LoadLotAsync(original.LotId);
        var mirrored = -original.Quantity;
        var before = lot.AvailableQuantity;

        if (mirrored < 0)
        {
            Withdraw(lot, -mirrored);
        }
        else
        {
            lot.QuantityRemaining += mirrored;
            if (lot.QuantityRemaining > lot.QuantityReceived)
            {
                lot.QuantityReceived = lot.QuantityRemaining;
            }
            SettleStatus(lot);
        }

        await AdjustInventoryCacheAsync(lot.ItemId, lot.LocationId, lot.AvailableQuantity - before);

        var actor = _currentUser.Current;

        _context.StockLedgers.Add(new StockLedger
        {
            LotId = lot.LotId,
            ItemId = lot.ItemId,
            LocationId = lot.LocationId,
            MovementType = MovementType.Reversal,
            Quantity = mirrored,
            UomId = lot.UomId,
            UnitCost = original.UnitCost,
            ReferenceType = original.ReferenceType,
            ReferenceId = original.ReferenceId,
            UserId = actor.UserId,
            UserName = actor.AuditName,
            PostedAt = DateTime.UtcNow,
            ReversalOfLedgerId = ledgerId,
            Notes = reason
        });
    }

    public async Task<decimal> ReconcileLotAsync(int lotId)
    {
        var lot = await LoadLotAsync(lotId);

        var fromLedger = await _context.StockLedgers
            .Where(l => l.LotId == lotId)
            .SumAsync(l => l.Quantity);

        return lot.QuantityRemaining - fromLedger;
    }

    // ---------- internals ----------

    /// <summary>
    /// Keeps <see cref="Inventory.CurrentStock"/> in step with the lots it summarises.
    /// </summary>
    /// <remarks>
    /// <see cref="Inventory"/> stopped being the source of truth in this task: <see cref="InventoryLot"/>
    /// is. But the existing screens and reports still read <c>Inventory.CurrentStock</c>, and rewriting
    /// every one of them to sum lots on every request was out of scope here. So the balance is kept as a
    /// projection, nudged by exactly the change in <see cref="InventoryLot.AvailableQuantity"/> that each
    /// posting call causes, rather than recomputed from scratch - recomputing under concurrent posting to
    /// the same item/location would race the same way the pre-Task-5 code did.
    /// <para>
    /// A zero delta is a no-op on purpose: a lot arriving as <c>Quarantine</c> changes
    /// <see cref="InventoryLot.QuantityRemaining"/> without changing <see cref="InventoryLot.AvailableQuantity"/>,
    /// and the cache must not move until the stock is actually available.
    /// </para>
    /// </remarks>
    private async Task AdjustInventoryCacheAsync(int itemId, int locationId, decimal delta)
    {
        if (delta == 0)
        {
            return;
        }

        // Checked against the change tracker before the database: two postings to the same item and
        // location within one unsaved unit of work (for example two receipts from different suppliers
        // landing in the same call) must share one row, not each race to insert their own and collide
        // on the unique (ItemId, LocationId) index when SaveChanges runs.
        var balance = _context.Inventories.Local
            .FirstOrDefault(i => i.ItemId == itemId && i.LocationId == locationId)
            ?? await _context.Inventories
                .FirstOrDefaultAsync(i => i.ItemId == itemId && i.LocationId == locationId);

        if (balance is null)
        {
            balance = new Inventory { ItemId = itemId, LocationId = locationId, CurrentStock = 0m };
            _context.Inventories.Add(balance);
        }

        balance.CurrentStock += delta;
    }

    private async Task<InventoryLot> LoadLotAsync(int lotId)
        => await _context.InventoryLots.FirstOrDefaultAsync(l => l.LotId == lotId)
           ?? throw new InvalidOperationException($"Lot {lotId} was not found.");

    /// <summary>
    /// Takes quantity out of a lot, refusing to overdraw.
    /// </summary>
    private static void Withdraw(InventoryLot lot, decimal quantity, bool settleStatus = true)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity), quantity, "A withdrawal must be for a positive quantity.");
        }

        if (lot.QuantityRemaining < quantity)
        {
            throw new InsufficientStockException(
                $"Lot {lot.LotCode} holds {lot.QuantityRemaining} but {quantity} was requested.");
        }

        lot.QuantityRemaining -= quantity;

        if (settleStatus)
        {
            SettleStatus(lot);
        }
    }

    /// <summary>
    /// Moves a fully drawn lot to Consumed, and brings one back to Available if a reversal refilled it.
    /// </summary>
    private static void SettleStatus(InventoryLot lot)
    {
        if (lot.QuantityRemaining == 0 && lot.Status == LotStatus.Available)
        {
            lot.Status = LotStatus.Consumed;
            return;
        }

        if (lot.QuantityRemaining > 0 && lot.Status == LotStatus.Consumed)
        {
            lot.Status = LotStatus.Available;
        }
    }

    private void TrySetStatus(InventoryLot lot, LotStatus target)
    {
        if (_statusGuard.CanTransition(lot.Status, target))
        {
            lot.Status = target;
        }
    }

    private void AddLedgerEntry(
        InventoryLot lot,
        MovementType movementType,
        decimal signedQuantity,
        string referenceType,
        string referenceId,
        string? notes = null)
    {
        var actor = _currentUser.Current;

        _context.StockLedgers.Add(new StockLedger
        {
            // Navigation rather than LotId: a lot created in this same unit of work has no id yet, and
            // EF fills the foreign key when it saves.
            Lot = lot,
            ItemId = lot.ItemId,
            LocationId = lot.LocationId,
            MovementType = movementType,
            Quantity = signedQuantity,
            UomId = lot.UomId,
            UnitCost = lot.UnitCost,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            UserId = actor.UserId,
            UserName = actor.AuditName,
            PostedAt = DateTime.UtcNow,
            Notes = notes
        });
    }
}
