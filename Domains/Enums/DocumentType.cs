namespace Domains.Enums;

/// <summary>
/// Kinds of document that carry a human-readable, gapless number.
/// </summary>
/// <remarks>
/// Members for documents that do not exist yet are declared up front so the numbering scheme is
/// settled in one place rather than invented separately by each later task.
/// </remarks>
public enum DocumentType
{
    PurchaseRequisition = 1,
    PurchaseOrder = 2,
    GoodsReceipt = 3,
    IncomingInspection = 4,
    NonConformance = 5,
    ReturnToVendor = 6,
    ProductionOrder = 7,
    Disposal = 8,
    BranchRequest = 9,
    Shipment = 10,
    DeliveryReceipt = 11,
    BranchReturn = 12,
    CycleCount = 13,
    StockAdjustment = 14,
    Item = 15,
    Supplier = 16,
    Recipe = 17,
    Delivery = 18,
    Discrepancy = 19,
    PutAway = 20,
    LossReport = 21
}

/// <summary>
/// The prefix each document number starts with, and the shape of the number itself.
/// </summary>
/// <remarks>
/// Numbers look like <c>PO-2026-0042</c>: prefix, calendar year, then a per-year sequence. The year is
/// part of the number so sequences restart annually, which is what people expect when reading a
/// document reference aloud.
/// <para>
/// Kept as an explicit switch rather than an attribute so that adding a document type without giving
/// it a prefix is a compile error, not a runtime surprise.
/// </para>
/// </remarks>
public static class DocumentNumbering
{
    /// <summary>Width of the zero-padded sequence portion.</summary>
    public const int SequenceWidth = 4;

    public static string PrefixFor(DocumentType documentType) => documentType switch
    {
        DocumentType.PurchaseRequisition => "PR",
        DocumentType.PurchaseOrder => "PO",
        DocumentType.GoodsReceipt => "GRN",
        DocumentType.IncomingInspection => "QC-IN",
        DocumentType.NonConformance => "NCR",
        DocumentType.ReturnToVendor => "RTV",
        DocumentType.ProductionOrder => "MB",
        DocumentType.Disposal => "WD",
        DocumentType.BranchRequest => "BR",
        DocumentType.Shipment => "SH",
        DocumentType.DeliveryReceipt => "DR",
        DocumentType.BranchReturn => "RET",
        DocumentType.CycleCount => "CC",
        DocumentType.StockAdjustment => "ADJ",
        DocumentType.Item => "SPL",
        DocumentType.Supplier => "SUP",
        DocumentType.Recipe => "BOM",
        DocumentType.Delivery => "DEL",
        DocumentType.Discrepancy => "DSC",
        DocumentType.PutAway => "PA",
        DocumentType.LossReport => "LR",
        _ => throw new ArgumentOutOfRangeException(
            nameof(documentType), documentType, "This document type has no number prefix.")
    };

    /// <summary>Builds the full number from its parts.</summary>
    public static string Format(DocumentType documentType, int year, int sequence)
        => $"{PrefixFor(documentType)}-{year}-{sequence.ToString().PadLeft(SequenceWidth, '0')}";
}
