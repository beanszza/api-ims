using System.Reflection;
using Domains.Exceptions;

namespace Domains.Enums;

/// <summary>
/// Translates between enum members and their stored/wire string form as declared by
/// <see cref="DbValueAttribute"/>.
/// </summary>
public static class EnumDbValue
{
    /// <summary>The canonical string written to the database and returned in API responses.</summary>
    public static string ToDbValue<TEnum>(TEnum value) where TEnum : struct, Enum
        => EnumDbValueCache<TEnum>.ToDbValue(value);

    /// <summary>
    /// Reads a stored or inbound string. Accepts the canonical value, any declared alias, and the
    /// C# member name, all case-insensitively.
    /// </summary>
    /// <exception cref="InvalidEnumDbValueException">The string is not a known member.</exception>
    public static TEnum Parse<TEnum>(string? raw) where TEnum : struct, Enum
        => EnumDbValueCache<TEnum>.Parse(raw);

    public static bool TryParse<TEnum>(string? raw, out TEnum value) where TEnum : struct, Enum
        => EnumDbValueCache<TEnum>.TryParse(raw, out value);

    /// <summary>Canonical strings for every member, in declaration order. Useful for validation messages.</summary>
    public static IReadOnlyList<string> AllDbValues<TEnum>() where TEnum : struct, Enum
        => EnumDbValueCache<TEnum>.AllDbValues;

    /// <summary>Comma separated list of accepted values, for error messages and API validation.</summary>
    public static string DescribeAccepted<TEnum>() where TEnum : struct, Enum
        => string.Join(", ", AllDbValues<TEnum>().Where(v => !string.IsNullOrEmpty(v)));
}

internal static class EnumDbValueCache<TEnum> where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> Forward = [];
    private static readonly Dictionary<string, TEnum> Reverse =
        new(StringComparer.OrdinalIgnoreCase);

    static EnumDbValueCache()
    {
        var members = new List<string>();

        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (TEnum)field.GetValue(null)!;
            var attribute = field.GetCustomAttribute<DbValueAttribute>();
            var dbValue = attribute?.Value ?? field.Name;

            Forward[value] = dbValue;
            members.Add(dbValue);

            // Canonical value, C# member name, and any declared alias all resolve on read.
            Reverse.TryAdd(dbValue, value);
            Reverse.TryAdd(field.Name, value);

            foreach (var alias in attribute?.Aliases ?? [])
            {
                Reverse.TryAdd(alias, value);
            }
        }

        AllDbValues = members;
    }

    public static IReadOnlyList<string> AllDbValues { get; }

    public static string ToDbValue(TEnum value) =>
        Forward.TryGetValue(value, out var dbValue)
            ? dbValue
            : throw new InvalidEnumDbValueException(typeof(TEnum), value.ToString());

    public static TEnum Parse(string? raw)
    {
        if (TryParse(raw, out var value))
        {
            return value;
        }

        throw new InvalidEnumDbValueException(
            typeof(TEnum),
            raw,
            string.Join(", ", AllDbValues.Where(v => !string.IsNullOrEmpty(v))));
    }

    public static bool TryParse(string? raw, out TEnum value)
    {
        // An empty column is a real state in the legacy schema: several entities default their
        // status to string.Empty. Enums that can encounter it declare a member mapped to "".
        var key = raw ?? string.Empty;

        if (Reverse.TryGetValue(key, out value))
        {
            return true;
        }

        if (key.Length > 0 && Reverse.TryGetValue(key.Trim(), out value))
        {
            return true;
        }

        value = default;
        return false;
    }
}
