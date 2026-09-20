using Domains.Enums;

namespace Domains.Exceptions;

/// <summary>
/// Thrown when a document is asked to move between two states that the lifecycle does not allow.
/// The message names both states and what would have been legal, so the caller can act on it.
/// </summary>
public sealed class InvalidStatusTransitionException : Exception
{
    private InvalidStatusTransitionException(
        string message,
        string documentType,
        string from,
        string to) : base(message)
    {
        DocumentType = documentType;
        From = from;
        To = to;
    }

    public string DocumentType { get; }
    public string From { get; }
    public string To { get; }

    public static InvalidStatusTransitionException Create<TEnum>(
        TEnum from,
        TEnum to,
        IReadOnlyCollection<TEnum> allowed) where TEnum : struct, Enum
    {
        var documentType = typeof(TEnum).Name.Replace("Status", string.Empty);
        var fromText = Describe(from);
        var toText = Describe(to);

        var message = allowed.Count == 0
            ? $"Cannot change {documentType} from '{fromText}' to '{toText}': '{fromText}' is a final state."
            : $"Cannot change {documentType} from '{fromText}' to '{toText}'. " +
              $"Allowed next: {string.Join(", ", allowed.Select(a => $"'{Describe(a)}'"))}.";

        return new InvalidStatusTransitionException(message, documentType, fromText, toText);
    }

    private static string Describe<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var dbValue = EnumDbValue.ToDbValue(value);
        return string.IsNullOrEmpty(dbValue) ? value.ToString() : dbValue;
    }
}
