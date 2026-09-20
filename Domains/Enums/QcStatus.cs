namespace Domains.Enums;

/// <summary>
/// Outcome of a quality inspection.
/// </summary>
/// <remarks>
/// Production uses Pending / Approved / Rejected on <c>ProductionBatch.QualityStatus</c>. Incoming
/// goods QA currently writes free text into <c>PurchaseOrder.QaStatus</c>; that field is replaced
/// wholesale by <c>QcIncoming</c> in Task 18, so it is deliberately left as a string for now rather
/// than forcing two different vocabularies into one enum.
/// </remarks>
public enum QcStatus
{
    /// <summary>Legacy rows created before a verdict was recorded.</summary>
    [DbValue("")]
    Unspecified = 0,

    [DbValue("Pending")]
    Pending = 1,

    /// <summary>Released for use. "Passed" is accepted on read for incoming-goods rows.</summary>
    [DbValue("Approved", Aliases = ["Passed"])]
    Approved = 2,

    /// <summary>Not fit for use. "Failed" is accepted on read for incoming-goods rows.</summary>
    [DbValue("Rejected", Aliases = ["Failed"])]
    Rejected = 3
}
