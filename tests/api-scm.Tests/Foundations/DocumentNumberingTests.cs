using api_scm.Tests.Infrastructure;
using Applications.Services;
using Domains.Enums;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Tests.Foundations;

[Collection(ScmDatabaseCollection.Name)]
public sealed class DocumentNumberingTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task The_first_number_of_a_year_is_one()
    {
        await using var context = CreateContext();
        var service = new DocumentNumberService(context);

        var number = await service.NextAsync(DocumentType.PurchaseOrder, new DateTime(2026, 3, 1));

        number.Should().Be("PO-2026-0001");
    }

    [Fact]
    public async Task Numbers_increment_and_stay_zero_padded()
    {
        await using var context = CreateContext();
        var service = new DocumentNumberService(context);
        var asOf = new DateTime(2026, 3, 1);

        var issued = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            issued.Add(await service.NextAsync(DocumentType.PurchaseOrder, asOf));
        }

        issued[0].Should().Be("PO-2026-0001");
        issued[8].Should().Be("PO-2026-0009");
        issued[9].Should().Be("PO-2026-0010");
        issued[11].Should().Be("PO-2026-0012");
    }

    [Fact]
    public async Task Sequences_restart_each_year_and_are_independent_per_document_type()
    {
        await using var context = CreateContext();
        var service = new DocumentNumberService(context);

        (await service.NextAsync(DocumentType.PurchaseOrder, new DateTime(2025, 12, 31)))
            .Should().Be("PO-2025-0001");
        (await service.NextAsync(DocumentType.PurchaseOrder, new DateTime(2026, 1, 1)))
            .Should().Be("PO-2026-0001", "a new year starts a new run");

        // A different document type keeps its own counter.
        (await service.NextAsync(DocumentType.GoodsReceipt, new DateTime(2026, 1, 1)))
            .Should().Be("GRN-2026-0001");
    }

    [Fact]
    public async Task Every_document_type_has_a_distinct_prefix()
    {
        var prefixes = Enum.GetValues<DocumentType>()
            .Select(DocumentNumbering.PrefixFor)
            .ToList();

        prefixes.Should().OnlyHaveUniqueItems(
            "two document types sharing a prefix would produce ambiguous references");
        prefixes.Should().NotContain(string.Empty);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task Concurrent_allocation_never_issues_the_same_number_twice()
    {
        // This is the property that matters. Each task uses its own context and its own connection, so
        // the serialisation has to come from the database, not from C#.
        const int callers = 25;
        var asOf = new DateTime(2026, 6, 1);

        var tasks = Enumerable.Range(0, callers).Select(async _ =>
        {
            await using var context = CreateContext();
            var service = new DocumentNumberService(context);
            return await service.NextAsync(DocumentType.PurchaseOrder, asOf);
        }).ToList();

        var numbers = await Task.WhenAll(tasks);

        numbers.Should().OnlyHaveUniqueItems("concurrent callers must never share a number");
        numbers.Should().HaveCount(callers);

        // Gapless as well as unique: exactly 1..callers were issued.
        numbers.Select(n => int.Parse(n.Split('-')[2]))
            .OrderBy(n => n)
            .Should().BeEquivalentTo(Enumerable.Range(1, callers));
    }

    [Fact]
    public async Task Only_one_sequence_row_exists_per_type_and_year()
    {
        await using var context = CreateContext();
        var service = new DocumentNumberService(context);
        var asOf = new DateTime(2026, 6, 1);

        for (var i = 0; i < 5; i++)
        {
            await service.NextAsync(DocumentType.PurchaseOrder, asOf);
        }

        await using var verify = CreateContext();
        var rows = await verify.DocumentSequences
            .Where(s => s.DocType == "PurchaseOrder" && s.Year == 2026)
            .ToListAsync();

        rows.Should().ContainSingle();
        rows[0].LastNumber.Should().Be(5);
    }
}
