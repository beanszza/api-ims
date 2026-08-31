namespace Domains.Enums;

/// <summary>
/// What happens to the rejected portion of an inspected receipt line.
/// </summary>
/// <remarks>
/// Introduced with <c>QcIncoming</c> in Task 18. This is what makes the "90 kg delivered, 80 good,
/// 10 mouldy" case recordable: the accepted quantity is released and the rejected quantity gets an
/// explicit, auditable destination instead of disappearing.
/// </remarks>
public enum QcDisposition
{
    /// <summary>Fit for use. Lot moves to Available.</summary>
    [DbValue("Accept")]
    Accept = 1,

    /// <summary>Out of spec but usable. Requires approval, typically at a negotiated discount.</summary>
    [DbValue("Accept With Concession")]
    AcceptWithConcession = 2,

    /// <summary>Send back to the supplier and expect a credit or replacement.</summary>
    [DbValue("Reject - Return")]
    RejectReturn = 3,

    /// <summary>Not worth returning. Written off on site at cost.</summary>
    [DbValue("Reject - Scrap")]
    RejectScrap = 4,

    /// <summary>Salvageable after treatment, for example re-cleaning or re-sorting.</summary>
    [DbValue("Rework")]
    Rework = 5,

    /// <summary>Verdict deferred pending a laboratory result. Stays in quarantine.</summary>
    [DbValue("Hold For Lab")]
    HoldForLab = 6
}
