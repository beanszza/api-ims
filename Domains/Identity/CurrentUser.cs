namespace Domains.Identity;

/// <summary>
/// Who is performing the current operation.
/// </summary>
/// <param name="UserId">
/// The auth service's subject identifier, or a <see cref="SystemUsers"/> sentinel. A string rather
/// than an integer because identity is owned by the central auth service, whose subject is an ASP.NET
/// Identity id, not a number local to this database.
/// </param>
/// <param name="DisplayName">
/// Human-readable name, captured alongside the id so an audit trail can be read without calling the
/// auth service, and remains readable if that user is later renamed or removed.
/// </param>
public sealed record CurrentUser(
    string UserId,
    string? DisplayName,
    string? Email,
    IReadOnlyList<string> Roles,
    bool IsAuthenticated)
{
    /// <summary>No identity on the request.</summary>
    public static CurrentUser Anonymous { get; } = new(
        SystemUsers.Unauthenticated, "Unauthenticated", null, [], false);

    /// <summary>A background job acting without a person behind it.</summary>
    public static CurrentUser Background { get; } = new(
        SystemUsers.System, "System", null, [], false);

    /// <summary>Name to store on audit records; never null, so trails are always readable.</summary>
    public string AuditName => string.IsNullOrWhiteSpace(DisplayName) ? UserId : DisplayName;

    public bool IsInRole(string role) =>
        Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}
