namespace Applications.Interfaces;

/// <summary>
/// Records who changed what. The single way audit entries are written.
/// </summary>
/// <remarks>
/// Previously only <c>StockTransferService</c> and <c>LocationService</c> wrote audit rows, each
/// building the entity by hand and supplying the user id from a different place. Centralising it means
/// attribution cannot be applied in one service and forgotten in another.
/// </remarks>
public interface IAuditTrail
{
    /// <summary>
    /// Stages an audit entry for the current user. Saved with the surrounding posting, so an audit row
    /// never survives an operation that rolled back.
    /// </summary>
    /// <param name="entityName">Entity type, for example "StockTransfer".</param>
    /// <param name="entityId">Identifier of the affected record.</param>
    /// <param name="action">What happened, for example "Created" or "StatusUpdated".</param>
    /// <param name="fieldName">Field that changed, when the action is a field-level edit.</param>
    void Record(
        string entityName,
        string entityId,
        string action,
        string? fieldName = null,
        string? oldValue = null,
        string? newValue = null);
}
