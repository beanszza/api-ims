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

    /// <summary>Auth service subject, or a <see cref="Domains.Identity.SystemUsers"/> sentinel.</summary>
    public string UserId { get; set; } = Domains.Identity.SystemUsers.Unauthenticated;

    /// <summary>Display name captured at the time of the action.</summary>
    public string UserName { get; set; } = string.Empty;
}
