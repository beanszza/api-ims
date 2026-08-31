namespace Domains.Exceptions;

/// <summary>
/// Thrown when an operation requires a known user and the request carries no identity.
/// </summary>
public sealed class NotAuthenticatedException : Exception
{
    public NotAuthenticatedException(string operation)
        : base($"{operation} requires an authenticated user, but the request carried no identity.")
    {
    }
}
