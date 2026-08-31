namespace Domains.Entities;


public class InventoryMovementLog
{
    public int MovementId { get; set; }
    public int ItemId { get; set; }
    public int LocationId { get; set; }

    /// <summary>
    /// Signed quantity change: negative for issues, positive for receipts.
    /// </summary>
    /// <remarks>
    /// Converted to decimal alongside the balances it records. Left as an integer, a 0.5 kg
    /// consumption would have been logged as 0 while the balance genuinely moved, so the ledger
    /// could not be reconciled against stock.
    /// </remarks>
    public decimal ChangeQuantity { get; set; }

    public string ActionType { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;

    /// <summary>
    /// Who posted the movement: the auth service subject, or a <see cref="Domains.Identity.SystemUsers"/>
    /// sentinel.
    /// </summary>
    /// <remarks>
    /// A string, not an int. Identity belongs to the central auth service and its subject is an
    /// ASP.NET Identity id, so an integer column could never hold a real one - which is why this used to
    /// be hardcoded to 1.
    /// </remarks>
    public string UserId { get; set; } = Domains.Identity.SystemUsers.Unauthenticated;

    /// <summary>Display name captured at the time, so the trail reads without calling the auth service.</summary>
    public string UserName { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public Item? Item { get; set; }
    public Location? Location { get; set; }
}