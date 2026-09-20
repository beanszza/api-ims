using Domains.Enums;

namespace Applications.Interfaces;

/// <summary>
/// Allocates human-readable document numbers such as <c>PO-2026-0042</c>.
/// </summary>
public interface IDocumentNumberService
{
    /// <summary>
    /// Reserves and returns the next number for a document type.
    /// </summary>
    /// <param name="documentType">Kind of document being numbered.</param>
    /// <param name="asOf">
    /// Date deciding which yearly sequence to draw from. Defaults to now. Pass the document's own date
    /// when back-dating, so a document dated last year does not take a number from this year's run.
    /// </param>
    /// <remarks>
    /// Enlists in the caller's transaction when there is one, which makes numbering gapless: if the
    /// document fails to save, the number is released rather than burnt. The trade-off is that
    /// concurrent creation of the same document type serialises on this row until the caller commits.
    /// At this system's volume that is the right way round, because auditors ask about missing numbers.
    /// </remarks>
    Task<string> NextAsync(DocumentType documentType, DateTime? asOf = null);

    /// <summary>
    /// Reserves the next number within an arbitrary scope, for sequences that are not per-year.
    /// </summary>
    /// <param name="scopeKey">
    /// Any stable string identifying the counter, for example <c>Lot:UBE:260619</c> for the lots of one
    /// item on one day.
    /// </param>
    /// <remarks>
    /// Shares the same atomic increment as <see cref="NextAsync"/>, so two concurrent callers can never
    /// be handed the same number. Used for lot codes, where the sequence resets per item per day rather
    /// than per year.
    /// </remarks>
    Task<int> NextInScopeAsync(string scopeKey);
}
