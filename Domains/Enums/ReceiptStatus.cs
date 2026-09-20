namespace Domains.Enums;

/// <summary>
/// Lifecycle of a goods receipt note. A GRN only creates stock when it is posted, and a posted GRN
/// is immutable.
/// </summary>
/// <remarks>Introduced with <c>Grn</c> in Task 17.</remarks>
public enum ReceiptStatus
{
    /// <summary>Being keyed in. Editable, no stock effect.</summary>
    [DbValue("Draft")]
    Draft = 1,

    /// <summary>Posted: quarantine lots created and PO received quantities incremented. Immutable.</summary>
    [DbValue("Posted")]
    Posted = 2,

    /// <summary>Reversed by a correcting document.</summary>
    [DbValue("Reversed")]
    Reversed = 3,

    /// <summary>Abandoned before posting.</summary>
    [DbValue("Cancelled")]
    Cancelled = 4
}
