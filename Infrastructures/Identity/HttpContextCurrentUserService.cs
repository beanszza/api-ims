using System.Security.Claims;
using Applications.Interfaces;
using Domains.Exceptions;
using Domains.Identity;
using Microsoft.AspNetCore.Http;

namespace Infrastructures.Identity;

/// <summary>
/// Reads the acting user from the claims on the current request.
/// </summary>
/// <remarks>
/// Claim names match what the central auth service issues (see its <c>AuthClaimsService</c>): OpenIddict
/// writes <c>sub</c>, <c>name</c>, <c>email</c> and <c>role</c>, plus <c>systems</c>, <c>permissions</c>
/// and <c>isSuperUser</c>. Both the OpenIddict short names and the longer ClaimTypes URIs are accepted,
/// because which one arrives depends on whether the token has been through claim-type mapping.
/// <para>
/// IMPORTANT: this only yields a real user once an authentication scheme populates
/// <c>HttpContext.User</c>. Until then every request resolves to
/// <see cref="CurrentUser.Anonymous"/> - which is recorded as such, rather than being disguised as a
/// real person the way the previous hardcoded <c>UserId = 1</c> was.
/// </para>
/// </remarks>
public sealed class HttpContextCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public CurrentUser Current
    {
        get
        {
            var principal = _httpContextAccessor.HttpContext?.User;

            if (principal?.Identity is null || !principal.Identity.IsAuthenticated)
            {
                return CurrentUser.Anonymous;
            }

            var subject = FirstClaim(principal, "sub", ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(subject))
            {
                // Authenticated but with no subject is a misconfigured token, not a user.
                return CurrentUser.Anonymous;
            }

            var roles = principal.FindAll(c => c.Type is "role" or ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new CurrentUser(
                UserId: subject,
                DisplayName: FirstClaim(principal, "name", ClaimTypes.Name),
                Email: FirstClaim(principal, "email", ClaimTypes.Email),
                Roles: roles,
                IsAuthenticated: true);
        }
    }

    public CurrentUser RequireAuthenticated()
    {
        var user = Current;
        return user.IsAuthenticated
            ? user
            : throw new NotAuthenticatedException("This operation");
    }

    private static string? FirstClaim(ClaimsPrincipal principal, params string[] claimTypes)
        => claimTypes
            .Select(principal.FindFirst)
            .FirstOrDefault(c => c is not null && !string.IsNullOrWhiteSpace(c.Value))
            ?.Value;
}
