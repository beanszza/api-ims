using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Applications.Services;

/// <summary>
/// Allocates document numbers using a single atomic upsert per number.
/// </summary>
/// <remarks>
/// The implementation plan called for <c>SELECT ... FOR UPDATE</c> followed by an update. An
/// <c>INSERT ... ON CONFLICT DO UPDATE ... RETURNING</c> gives the same serialisation guarantee in one
/// round trip and, unlike select-then-update, also handles the very first number of a year without a
/// separate insert path and without a race between two callers both finding no row.
/// </remarks>
public sealed class DocumentNumberService : IDocumentNumberService
{
    private readonly ScmDbContext _context;

    public DocumentNumberService(ScmDbContext context) => _context = context;

    public async Task<string> NextAsync(DocumentType documentType, DateTime? asOf = null)
    {
        var year = (asOf ?? DateTime.UtcNow).Year;
        var docType = EnumDbValue.ToDbValue(documentType);

        var sequence = _context.Database.IsRelational()
            ? await NextRelationalAsync(docType, year)
            : await NextInMemoryAsync(docType, year);

        return DocumentNumbering.Format(documentType, year, sequence);
    }

    /// <summary>
    /// Arbitrary-scope counter. Reuses the same row and the same atomic increment as document numbers,
    /// with <c>Year = 0</c> marking the sequence as not year-scoped.
    /// </summary>
    public async Task<int> NextInScopeAsync(string scopeKey)
    {
        if (string.IsNullOrWhiteSpace(scopeKey))
        {
            throw new ArgumentException("A scope key is required.", nameof(scopeKey));
        }

        const int notYearScoped = 0;

        return _context.Database.IsRelational()
            ? await NextRelationalAsync(scopeKey, notYearScoped)
            : await NextInMemoryAsync(scopeKey, notYearScoped);
    }

    /// <summary>
    /// Increments and reads the counter in one statement. Concurrent callers block on the conflicting
    /// row rather than both reading the same value.
    /// </summary>
    private async Task<int> NextRelationalAsync(string docType, int year)
    {
        // "Value" is the column name Database.SqlQuery expects to project a scalar.
        var results = await _context.Database
            .SqlQuery<int>($"""
                INSERT INTO "DocumentSequences" ("DocType", "Year", "LastNumber")
                VALUES ({docType}, {year}, 1)
                ON CONFLICT ("DocType", "Year")
                DO UPDATE SET "LastNumber" = "DocumentSequences"."LastNumber" + 1
                RETURNING "LastNumber" AS "Value"
                """)
            .ToListAsync();

        return results.Single();
    }

    /// <summary>
    /// Fallback for the in-memory provider used by local development. Not concurrency safe, and does
    /// not need to be: the in-memory database is single-process and throwaway.
    /// </summary>
    private async Task<int> NextInMemoryAsync(string docType, int year)
    {
        var row = await _context.DocumentSequences
            .FirstOrDefaultAsync(s => s.DocType == docType && s.Year == year);

        if (row is null)
        {
            row = new DocumentSequence { DocType = docType, Year = year, LastNumber = 0 };
            _context.DocumentSequences.Add(row);
        }

        row.LastNumber++;
        await _context.SaveChangesAsync();
        return row.LastNumber;
    }
}
