namespace Domains.Enums;

/// <summary>
/// Declares the exact string a given enum member is stored as, and sent over the wire as.
/// </summary>
/// <remarks>
/// The legacy schema stores statuses as free text, and several of those strings contain spaces or
/// brackets ("QA Review", "In Progress", "Transfer Completed (No Addition)"), so they cannot be
/// expressed as C# identifiers. This attribute keeps the database and API representation byte for
/// byte identical while the code gains a closed set of values, which means introducing enums is not
/// a breaking change for the existing database or the existing frontend.
/// </remarks>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class DbValueAttribute(string value) : Attribute
{
    public string Value { get; } = value;

    /// <summary>
    /// Additional strings accepted when reading, but never written. Used to absorb historical
    /// spellings without keeping them alive in new data.
    /// </summary>
    public string[] Aliases { get; init; } = [];
}
