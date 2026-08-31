namespace Domains.Exceptions;

/// <summary>
/// Thrown when an operation would take more stock than a location holds.
/// </summary>
/// <remarks>
/// An exception rather than a returned failure, because the check happens inside a posting
/// transaction: throwing rolls back whatever the operation had already staged, which is exactly the
/// behaviour wanted. Callers translate it into a user-facing message.
/// </remarks>
public sealed class InsufficientStockException : Exception
{
    public InsufficientStockException(string message) : base(message)
    {
    }

    public static InsufficientStockException ForLocation(
        string itemName, decimal requested, decimal available, string unit = "")
        => new($"Insufficient stock for {itemName}. Requested {requested}{unit}, available {available}{unit}.");
}
