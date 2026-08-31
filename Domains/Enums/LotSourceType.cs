namespace Domains.Enums;

/// <summary>
/// Where a stock lot came from. Determines which traceability link is populated on it.
/// </summary>
public enum LotSourceType
{
    /// <summary>
    /// Received from a supplier. Carries <c>SupplierId</c>, the supplier's own lot number, and the
    /// receipt line that created it, which is what makes bad-supplier attribution possible.
    /// </summary>
    [DbValue("Purchased")]
    Purchased = 1,

    /// <summary>
    /// Made in-house. Carries the production order, and its lot code is the traceability lot code
    /// printed on the jar.
    /// </summary>
    [DbValue("Produced")]
    Produced = 2,

    /// <summary>
    /// Created when the old single-balance-per-location stock was converted into lots. Has no supplier
    /// and no expiry, because that information was never recorded.
    /// </summary>
    [DbValue("Opening Balance")]
    OpeningBalance = 3,

    /// <summary>Came back from a branch and was accepted into stock again.</summary>
    [DbValue("Branch Return")]
    BranchReturn = 4,

    /// <summary>Created by a counted adjustment where stock was found without a known origin.</summary>
    [DbValue("Adjustment")]
    Adjustment = 5
}
