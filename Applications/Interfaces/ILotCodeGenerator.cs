namespace Applications.Interfaces;

/// <summary>
/// Generates the human-readable codes that identify stock lots.
/// </summary>
public interface ILotCodeGenerator
{
    /// <summary>
    /// A code for a lot of purchased material, shaped <c>L-{yyMMdd}-{ITEM}-{nn}</c>, for example
    /// <c>L-260619-UBE-01</c>.
    /// </summary>
    /// <param name="itemId">Item the lot holds; its name supplies the abbreviation.</param>
    /// <param name="receivedOn">Date the lot was received, which forms the date part of the code.</param>
    Task<string> ForPurchasedLotAsync(int itemId, DateTime receivedOn);

    /// <summary>
    /// A traceability lot code for produced goods, shaped <c>{VARIANT}-{yyMMdd}-{nn}</c>, for example
    /// <c>UH500-260619-01</c>.
    /// </summary>
    /// <remarks>
    /// This is the code printed on the jar and the one a recall searches on, which is why it leads with
    /// the product rather than a generic "L" prefix.
    /// </remarks>
    Task<string> ForProducedLotAsync(string variantCode, DateTime producedOn);

    /// <summary>A code for a lot created by converting a legacy balance.</summary>
    string ForOpeningBalance(int itemId, int locationId);
}
