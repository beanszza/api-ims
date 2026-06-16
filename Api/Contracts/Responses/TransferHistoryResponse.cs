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
    public int UserId { get; set; }
}
