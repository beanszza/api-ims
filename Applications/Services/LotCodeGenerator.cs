using System.Text;
using Applications.Interfaces;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Applications.Services;

/// <summary>
/// Builds lot codes from a date, an item abbreviation, and a per-item-per-day sequence.
/// </summary>
/// <remarks>
/// The sequence comes from the same atomic counter that issues document numbers, so two deliveries of
/// the same item booked in at the same moment cannot receive the same code. Deriving the sequence from
/// <c>MAX(existing code)</c> would have been simpler and would have raced.
/// </remarks>
public sealed class LotCodeGenerator : ILotCodeGenerator
{
    /// <summary>Characters in the item abbreviation portion.</summary>
    private const int AbbreviationLength = 4;

    private readonly ScmDbContext _context;
    private readonly IDocumentNumberService _sequences;

    public LotCodeGenerator(ScmDbContext context, IDocumentNumberService sequences)
    {
        _context = context;
        _sequences = sequences;
    }

    public async Task<string> ForPurchasedLotAsync(int itemId, DateTime receivedOn)
    {
        var itemName = await _context.Items
            .Where(i => i.ItemId == itemId)
            .Select(i => i.ItemName)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Item {itemId} was not found, so no lot code can be built.");

        var abbreviation = Abbreviate(itemName);
        var datePart = receivedOn.ToString("yyMMdd");

        var sequence = await _sequences.NextInScopeAsync($"Lot:{abbreviation}:{datePart}");

        return $"L-{datePart}-{abbreviation}-{sequence:D2}";
    }

    public async Task<string> ForProducedLotAsync(string variantCode, DateTime producedOn)
    {
        if (string.IsNullOrWhiteSpace(variantCode))
        {
            throw new ArgumentException("A variant code is required to build a traceability lot code.",
                nameof(variantCode));
        }

        var code = Sanitise(variantCode);
        var datePart = producedOn.ToString("yyMMdd");

        var sequence = await _sequences.NextInScopeAsync($"FgLot:{code}:{datePart}");

        return $"{code}-{datePart}-{sequence:D2}";
    }

    public string ForOpeningBalance(int itemId, int locationId)
        => $"L-OPENING-{itemId}-{locationId}";

    /// <summary>
    /// Reduces an item name to a short uppercase token: "Ube Halaya 500g" becomes "UBEH".
    /// </summary>
    /// <remarks>
    /// Only letters and digits survive, so punctuation and spacing cannot produce a code that is awkward
    /// to read aloud or type into a search box. Collisions between different items are harmless: the
    /// sequence is scoped to the abbreviation, so two items sharing one still get distinct codes.
    /// </remarks>
    private static string Abbreviate(string itemName)
    {
        var builder = new StringBuilder(AbbreviationLength);

        foreach (var character in itemName)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }

            if (builder.Length == AbbreviationLength)
            {
                break;
            }
        }

        return builder.Length > 0 ? builder.ToString() : "ITEM";
    }

    private static string Sanitise(string code)
    {
        var builder = new StringBuilder(code.Length);

        foreach (var character in code)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.Length > 0 ? builder.ToString() : "FG";
    }
}
