using Applications.Interfaces;
using Domains.Enums;
using Domains.Exceptions;

namespace Applications.Services;

/// <summary>
/// Explicit state machines for every guarded document lifecycle.
/// </summary>
/// <remarks>
/// The maps are deliberately built from what the existing UI actually does, so turning the guard on
/// blocks nonsense without blocking real workflows. Two rules apply throughout:
/// <list type="bullet">
///   <item>Re-applying the current status is always allowed and is a no-op, because several callers
///   re-save an unchanged status.</item>
///   <item><c>Unspecified</c> is a legacy-only source state: rows written before statuses existed
///   need a way out, but nothing may ever transition back into it.</item>
/// </list>
/// </remarks>
public sealed class StatusTransitionGuard : IStatusTransitionGuard
{
    private static readonly Dictionary<Type, object> Maps = new()
    {
        [typeof(PurchaseOrderStatus)] = BuildPurchaseOrderMap(),
        [typeof(ShipmentStatus)] = BuildShipmentMap(),
        [typeof(BatchStatus)] = BuildBatchMap(),
        [typeof(QcStatus)] = BuildQcMap(),
        [typeof(ReceiptStatus)] = BuildReceiptMap(),
        [typeof(LotStatus)] = BuildLotMap(),
        [typeof(ApprovalStatus)] = BuildApprovalMap(),
        [typeof(DeliveryStatus)] = BuildDeliveryMap()
    };

    public bool CanTransition<TEnum>(TEnum from, TEnum to) where TEnum : struct, Enum
    {
        if (EqualityComparer<TEnum>.Default.Equals(from, to))
        {
            return true;
        }

        return AllowedFrom(from).Contains(to);
    }

    public void EnsureCanTransition<TEnum>(TEnum from, TEnum to) where TEnum : struct, Enum
    {
        if (!CanTransition(from, to))
        {
            throw InvalidStatusTransitionException.Create(from, to, AllowedFrom(from));
        }
    }

    public IReadOnlyCollection<TEnum> AllowedFrom<TEnum>(TEnum from) where TEnum : struct, Enum
    {
        var map = MapFor<TEnum>();
        return map.TryGetValue(from, out var allowed) ? allowed : [];
    }

    public bool IsFinal<TEnum>(TEnum status) where TEnum : struct, Enum
        => AllowedFrom(status).Count == 0;

    private static Dictionary<TEnum, TEnum[]> MapFor<TEnum>() where TEnum : struct, Enum
    {
        if (Maps.TryGetValue(typeof(TEnum), out var map))
        {
            return (Dictionary<TEnum, TEnum[]>)map;
        }

        throw new InvalidOperationException(
            $"{typeof(TEnum).Name} has no registered transition map. Either it is not a document " +
            "lifecycle (LocationType, MovementType and QcDisposition are classifications, not " +
            $"lifecycles), or a map must be added to {nameof(StatusTransitionGuard)}.");
    }

    /// <summary>
    /// Full PO approval workflow:
    /// Draft → PendingApproval → Approved → Ordered (or Arrived for legacy delivery flow)
    /// PendingApproval → Rejected | Returned → Draft (re-submit)
    /// Legacy: Pending → Arrived → Completed
    /// </summary>
    private static Dictionary<PurchaseOrderStatus, PurchaseOrderStatus[]> BuildPurchaseOrderMap() => new()
    {
        // Legacy unspecified rows get a way out
        [PurchaseOrderStatus.Unspecified] =
        [
            PurchaseOrderStatus.Pending,
            PurchaseOrderStatus.Draft,
            PurchaseOrderStatus.Cancelled
        ],
        // --- New approval workflow ---
        [PurchaseOrderStatus.Draft] =
        [
            PurchaseOrderStatus.PendingApproval,
            PurchaseOrderStatus.Cancelled
        ],
        [PurchaseOrderStatus.PendingApproval] =
        [
            PurchaseOrderStatus.Approved,
            PurchaseOrderStatus.Rejected,
            PurchaseOrderStatus.Returned,
            PurchaseOrderStatus.Cancelled
        ],
        [PurchaseOrderStatus.Returned] =
        [
            PurchaseOrderStatus.PendingApproval,
            PurchaseOrderStatus.Cancelled
        ],
        [PurchaseOrderStatus.Approved] =
        [
            PurchaseOrderStatus.Ordered,
            PurchaseOrderStatus.Arrived,   // bridge to legacy delivery flow
            PurchaseOrderStatus.Cancelled
        ],
        [PurchaseOrderStatus.Ordered] = [],
        // --- Legacy procurement flow (kept for backward compat) ---
        [PurchaseOrderStatus.Pending] =
        [
            PurchaseOrderStatus.Arrived,
            PurchaseOrderStatus.Cancelled
        ],
        [PurchaseOrderStatus.Arrived] =
        [
            PurchaseOrderStatus.Completed,
            PurchaseOrderStatus.Rejected,
            PurchaseOrderStatus.Cancelled
        ],
        [PurchaseOrderStatus.Completed] = [],
        [PurchaseOrderStatus.Rejected] = [],
        [PurchaseOrderStatus.Cancelled] = []
    };


    /// <summary>
    /// Pending -> In Transit -> Completed. Closes a hole in the old code, where Pending -> Completed
    /// fell through every side-effect branch and marked a transfer delivered without debiting anything.
    /// </summary>
    private static Dictionary<ShipmentStatus, ShipmentStatus[]> BuildShipmentMap() => new()
    {
        [ShipmentStatus.Unspecified] = [ShipmentStatus.Pending, ShipmentStatus.Cancelled],
        [ShipmentStatus.Pending] = [ShipmentStatus.InTransit, ShipmentStatus.Cancelled],
        [ShipmentStatus.InTransit] = [ShipmentStatus.Completed, ShipmentStatus.Cancelled],
        [ShipmentStatus.Completed] = [],
        [ShipmentStatus.Cancelled] = []
    };

    private static Dictionary<BatchStatus, BatchStatus[]> BuildBatchMap() => new()
    {
        [BatchStatus.Unspecified] = [BatchStatus.Scheduled, BatchStatus.Cancelled],
        [BatchStatus.Scheduled] = [BatchStatus.Approved, BatchStatus.InProgress, BatchStatus.Rejected, BatchStatus.Cancelled],
        [BatchStatus.Approved] = [BatchStatus.InProgress, BatchStatus.Cancelled],
        [BatchStatus.InProgress] =
        [
            BatchStatus.PassedQa,
            BatchStatus.Rejected,
            BatchStatus.Completed,
            BatchStatus.Cancelled
        ],
        // A released batch still has to be packaged and posted to stock.
        [BatchStatus.PassedQa] =
        [
            BatchStatus.Completed,
            BatchStatus.InventoryAdded,
            BatchStatus.Rejected
        ],
        // The cook can finish before QA is recorded, so Completed is not yet terminal.
        [BatchStatus.Completed] = [BatchStatus.PassedQa, BatchStatus.Rejected, BatchStatus.InventoryAdded],
        [BatchStatus.Rejected] = [],
        [BatchStatus.InventoryAdded] = [],
        [BatchStatus.Cancelled] = []
    };

    private static Dictionary<QcStatus, QcStatus[]> BuildQcMap() => new()
    {
        [QcStatus.Unspecified] = [QcStatus.Pending, QcStatus.Approved, QcStatus.Rejected],
        [QcStatus.Pending] = [QcStatus.Approved, QcStatus.Rejected],
        // A later finding can fail something already released; the reverse is a fresh inspection.
        [QcStatus.Approved] = [QcStatus.Rejected],
        [QcStatus.Rejected] = []
    };

    private static Dictionary<ReceiptStatus, ReceiptStatus[]> BuildReceiptMap() => new()
    {
        [ReceiptStatus.Draft] = [ReceiptStatus.Posted, ReceiptStatus.Cancelled],
        // A posted receipt is immutable: it can only be undone by a reversing document.
        [ReceiptStatus.Posted] = [ReceiptStatus.Reversed],
        [ReceiptStatus.Reversed] = [],
        [ReceiptStatus.Cancelled] = []
    };

    private static Dictionary<LotStatus, LotStatus[]> BuildLotMap() => new()
    {
        [LotStatus.Quarantine] =
        [
            LotStatus.Available,
            LotStatus.Rejected,
            LotStatus.OnHold,
            LotStatus.Disposed
        ],
        [LotStatus.Available] =
        [
            LotStatus.OnHold,
            LotStatus.Expired,
            LotStatus.Consumed,
            LotStatus.Disposed,
            LotStatus.Returned
        ],
        [LotStatus.OnHold] =
        [
            LotStatus.Available,
            LotStatus.Expired,
            LotStatus.Disposed,
            LotStatus.Returned
        ],
        [LotStatus.Expired] = [LotStatus.OnHold, LotStatus.Disposed],
        // Accept-with-concession can release rejected material back into use.
        [LotStatus.Rejected] = [LotStatus.Available, LotStatus.Returned, LotStatus.Disposed],
        [LotStatus.Consumed] = [],
        [LotStatus.Returned] = [],
        [LotStatus.Disposed] = []
    };

    private static Dictionary<ApprovalStatus, ApprovalStatus[]> BuildApprovalMap() => new()
    {
        [ApprovalStatus.Pending] =
        [
            ApprovalStatus.Approved,
            ApprovalStatus.Rejected,
            ApprovalStatus.Withdrawn
        ],
        [ApprovalStatus.Approved] = [],
        [ApprovalStatus.AutoApproved] = [],
        [ApprovalStatus.Rejected] = [],
        [ApprovalStatus.Withdrawn] = []
    };

    private static Dictionary<DeliveryStatus, DeliveryStatus[]> BuildDeliveryMap() => new()
    {
        [DeliveryStatus.Scheduled] = [DeliveryStatus.InTransit, DeliveryStatus.Arrived, DeliveryStatus.Cancelled],
        [DeliveryStatus.InTransit] = [DeliveryStatus.Arrived, DeliveryStatus.Cancelled],
        [DeliveryStatus.Arrived] = [],
        [DeliveryStatus.Cancelled] = []
    };
}
