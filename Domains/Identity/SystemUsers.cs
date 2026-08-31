namespace Domains.Identity;

/// <summary>
/// Reserved user identifiers for actors that are not a signed-in person.
/// </summary>
/// <remarks>
/// Attribution has to be honest. Previously every posting recorded <c>UserId = 1</c>, so the audit
/// trail asserted a specific person had done things nobody could verify. These sentinels say what
/// actually happened instead, and because they are prefixed they can never collide with a real
/// identity subject from the auth service.
/// </remarks>
public static class SystemUsers
{
    /// <summary>No identity was presented with the request.</summary>
    public const string Unauthenticated = "anonymous";

    /// <summary>A background job or scheduled process acted on its own.</summary>
    public const string System = "system";

    /// <summary>A database migration or seeder acted.</summary>
    public const string Migration = "migration";

    /// <summary>
    /// An identifier for a row written before real attribution existed, preserving whatever integer
    /// the old code recorded so the history is not silently rewritten.
    /// </summary>
    public static string Legacy(int legacyUserId) => $"legacy:{legacyUserId}";

    /// <summary>True when the identifier denotes a system actor rather than a person.</summary>
    public static bool IsSystemActor(string? userId) =>
        userId is Unauthenticated or System or Migration
        || (userId?.StartsWith("legacy:", StringComparison.Ordinal) ?? false);
}
