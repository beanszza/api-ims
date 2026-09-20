using System;

namespace api_scm.Contracts.Responses;

public class TransferHistoryResponse
{
    public int LogId { get; set; }
    public string TransferId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    /// <summary>Auth subject or system sentinel.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Display name captured when the change was made.</summary>
    public string UserName { get; set; } = string.Empty;
}
