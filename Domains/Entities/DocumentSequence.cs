namespace Domains.Entities;

/// <summary>
/// The last number issued for a document type in a given year.
/// </summary>
/// <remarks>
/// One row per (document type, year), with a unique index enforcing that. Numbers are allocated by an
/// atomic upsert that increments and returns <see cref="LastNumber"/> in a single statement, so two
/// concurrent requests cannot be handed the same number.
/// <para>
/// Deliberately separate from the identity keys. A business document number is read aloud, printed,
/// and quoted in disputes, so it must not be derived from a surrogate primary key that could change
/// meaning if data is ever migrated.
/// </para>
/// </remarks>
public class DocumentSequence
{
    public int DocumentSequenceId { get; set; }

    /// <summary>The document type, stored as its enum name.</summary>
    public string DocType { get; set; } = string.Empty;

    /// <summary>Calendar year the sequence belongs to. Sequences restart each year.</summary>
    public int Year { get; set; }

    /// <summary>Highest number issued so far for this type and year.</summary>
    public int LastNumber { get; set; }
}
