using Domains.Enums;

namespace Domains.Entities;

/// <summary>
/// An append-only record of every quantity change to every lot.
/// </summary>
/// <remarks>
/// The explanation behind every balance. Given the ledger, any lot's remaining quantity is the sum of
/// its rows, which means a disagreement between the two is detectable rather than invisible.
/// <para>
/// Rows are never updated or deleted. A mistake is corrected by posting a reversing row that points at
/// the original through <see cref="ReversalOfLedgerId"/>, so the history shows both what was believed
/// and what was corrected. Editing history would destroy the only evidence of what actually happened.
/// </para>
/// <para>
/// Replaces <c>InventoryMovementLog</c>, whose <c>ActionType</c> was free text with a vocabulary that
/// drifted between services ("IN", "OUT", "Order Arrival", "Transfer Out",
/// "Transfer Completed (No Addition)"), and which had no lot reference at all.
/// </para>
/// </remarks>
public class StockLedger
{
    public long LedgerId { get; set; }

    /// <summary>The lot whose quantity moved. Present on every row: stock always belongs to a lot.</summary>
    public int LotId { get; set; }

    /// <summary>Denormalised from the lot so reports can group without a join.</summary>
    public int ItemId { get; set; }

    /// <summary>Denormalised from the lot: where the movement happened.</summary>
    public int LocationId { get; set; }

    public MovementType MovementType { get; set; }

    /// <summary>
    /// Signed change: negative for issues, positive for receipts.
    /// </summary>
    /// <remarks>
    /// Signed rather than a magnitude plus a direction flag, so a balance is a plain SUM and cannot be
    /// got wrong by forgetting to apply the sign.
    /// </remarks>
    public decimal Quantity { get; set; }

    public int UomId { get; set; }

    /// <summary>Cost per unit at the moment of the movement, for valuation and cost of waste.</summary>
    public decimal UnitCost { get; set; }

    /// <summary>
    /// What caused the movement, for example "PurchaseOrder" or "ProductionOrder".
    /// </summary>
    public string ReferenceType { get; set; } = string.Empty;

    /// <summary>
    /// The causing document's human-readable number, for example <c>PO-2026-0042</c>.
    /// </summary>
    /// <remarks>
    /// The document number rather than a surrogate key, so a ledger row is meaningful when read on its
    /// own or exported to a spreadsheet during an audit.
    /// </remarks>
    public string ReferenceId { get; set; } = string.Empty;

    /// <summary>Auth subject of whoever posted it, or a system sentinel.</summary>
    public string UserId { get; set; } = Domains.Identity.SystemUsers.Unauthenticated;

    /// <summary>Display name captured at the time.</summary>
    public string UserName { get; set; } = string.Empty;

    public DateTime PostedAt { get; set; }

    /// <summary>Set when this row reverses an earlier one, linking the correction to what it corrects.</summary>
    public long? ReversalOfLedgerId { get; set; }

    /// <summary>Free-text explanation, required for adjustments and disposals.</summary>
    public string? Notes { get; set; }

    public InventoryLot? Lot { get; set; }
    public Item? Item { get; set; }
    public Location? Location { get; set; }
    public StockLedger? ReversalOf { get; set; }
}
