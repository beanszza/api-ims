namespace api_scm.Contracts.Responses;

/// <summary>Metadata returned for linking an item to the order module.</summary>
public sealed class ItemOrderLinkData
{
    public string TargetModule { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? ItemName { get; set; }
}
