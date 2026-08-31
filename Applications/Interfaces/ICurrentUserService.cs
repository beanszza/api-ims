using Domains.Identity;

namespace Applications.Interfaces;

/// <summary>
/// Resolves who is acting on the current request.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// The acting user. Never null: an unauthenticated request resolves to
    /// <see cref="CurrentUser.Anonymous"/> rather than throwing, so read operations keep working while
    /// still being recorded truthfully.
    /// </summary>
    CurrentUser Current { get; }

    /// <summary>
    /// Returns the acting user, or throws when the request carries no identity.
    /// </summary>
    /// <exception cref="Domains.Exceptions.NotAuthenticatedException">No identity on the request.</exception>
    CurrentUser RequireAuthenticated();
}
