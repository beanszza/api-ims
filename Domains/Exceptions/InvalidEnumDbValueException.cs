namespace Domains.Exceptions;

/// <summary>
/// Thrown when a stored or inbound string is not a member of the target enum. Deliberately loud:
/// a status the system does not understand is a data problem, and silently coercing it to a default
/// is how wrong numbers reach reports.
/// </summary>
public sealed class InvalidEnumDbValueException : Exception
{
    public InvalidEnumDbValueException(Type enumType, string? rawValue, string? accepted = null)
        : base(BuildMessage(enumType, rawValue, accepted))
    {
        EnumType = enumType;
        RawValue = rawValue;
    }

    public Type EnumType { get; }
    public string? RawValue { get; }

    private static string BuildMessage(Type enumType, string? rawValue, string? accepted)
    {
        var display = rawValue is null ? "<null>" : $"'{rawValue}'";
        var message = $"{display} is not a valid {enumType.Name}.";
        return accepted is { Length: > 0 } ? $"{message} Accepted values: {accepted}." : message;
    }
}
