namespace Domains.Enums;

/// <summary>
/// Reason a stock movement was posted.
/// </summary>
/// <remarks>
/// Replaces the free-text <c>InventoryMovementLog.ActionType</c>, whose vocabulary drifted across
/// services: "IN", "OUT", "Order Arrival", "Transfer Out", "Transfer Cancelled" and
/// "Transfer Completed (No Addition)" were all in use, so grouping a report by action type could not
/// be trusted. The legacy strings are kept as read aliases so historical rows still load.
/// <para>
/// The column is converted and the history normalised in Task 9, when
/// <c>IStockPostingService</c> becomes the only writer of stock movements.
/// </para>
/// </remarks>
public enum MovementType
{
    /// <summary>Goods received from a supplier.</summary>
    [DbValue("Purchase Receipt", Aliases = ["Order Arrival", "IN"])]
    PurchaseReceipt = 1,

    /// <summary>Raw material or packaging drawn into a production batch.</summary>
    [DbValue("Production Consumption", Aliases = ["OUT"])]
    ProductionConsumption = 2,

    /// <summary>Finished goods posted from a completed batch.</summary>
    [DbValue("Production Output")]
    ProductionOutput = 3,

    /// <summary>Debit at the source when a shipment is dispatched.</summary>
    [DbValue("Transfer Out", Aliases = ["Transfer Completed (No Addition)"])]
    TransferOut = 4,

    /// <summary>Credit at the destination when receipt is confirmed.</summary>
    [DbValue("Transfer In")]
    TransferIn = 5,

    /// <summary>Stock returned to the source because a shipment was cancelled.</summary>
    [DbValue("Transfer Cancelled")]
    TransferCancelled = 6,

    /// <summary>Correction from a cycle count or physical inventory.</summary>
    [DbValue("Adjustment")]
    Adjustment = 7,

    /// <summary>Written off: expired, spoiled, damaged or scrapped.</summary>
    [DbValue("Disposal")]
    Disposal = 8,

    /// <summary>Returned to the supplier after failing incoming QA.</summary>
    [DbValue("Return To Vendor")]
    ReturnToVendor = 9,

    /// <summary>Stock coming back from a branch.</summary>
    [DbValue("Branch Return")]
    BranchReturn = 10,

    /// <summary>Reverses an earlier movement. Always references the row it undoes.</summary>
    [DbValue("Reversal")]
    Reversal = 11,

    /// <summary>Opening balance created when legacy quantities were converted into lots.</summary>
    [DbValue("Opening Balance")]
    OpeningBalance = 12
}
