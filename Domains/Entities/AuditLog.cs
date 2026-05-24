namespace Domains.Entities;

/// <summary>
/// Field-level audit log per ERD. Not mapped in EF until an AuditLog table migration exists in scm_db.
/// </summary>
public class AuditLog
{
    public int LogId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int UserId { get; set; }
}
