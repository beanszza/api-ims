using Applications.Interfaces;
using Domains.Entities;
using Infrastructures.Persistence;

namespace Applications.Services;

/// <summary>
/// Writes audit entries into the local <c>AuditLogs</c> table, attributed to the current user.
/// </summary>
/// <remarks>
/// Entries are staged on the DbContext rather than saved immediately, so they commit with the operation
/// they describe. An audit row claiming something happened, when the transaction around it rolled back,
/// would be worse than no row at all.
/// <para>
/// The platform has a dedicated audit-logs service. Forwarding to it is deliberately not wired up here:
/// its request contract has not been confirmed, and inventing one would produce entries that service
/// cannot read. The local table remains the system of record until that contract is agreed.
/// </para>
/// </remarks>
public sealed class AuditTrail : IAuditTrail
{
    private readonly ScmDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AuditTrail(ScmDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public void Record(
        string entityName,
        string entityId,
        string action,
        string? fieldName = null,
        string? oldValue = null,
        string? newValue = null)
    {
        var user = _currentUser.Current;

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            FieldName = fieldName ?? string.Empty,
            OldValue = oldValue ?? string.Empty,
            NewValue = newValue ?? string.Empty,
            Timestamp = DateTime.UtcNow,
            UserId = user.UserId,
            UserName = user.AuditName
        });
    }
}
