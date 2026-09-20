using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Tests.Infrastructure;

/// <summary>
/// Shared per-assembly database. Migrated once, then truncated between tests so each test starts
/// from a known empty schema without paying the migration cost repeatedly.
/// </summary>
public sealed class ScmDatabaseFixture : IAsyncLifetime
{
    private PostgresTestDatabase _database = null!;
    private string[] _tableNames = [];

    public string ConnectionString => _database.ConnectionString;

    public async Task InitializeAsync()
    {
        if (!await PostgresTestDatabase.IsServerAvailableAsync())
        {
            throw new InvalidOperationException(
                "No PostgreSQL server reachable for tests. Start the local PostgreSQL service, or set " +
                "the SCM_TEST_PG environment variable to an admin connection string, for example: " +
                "\"Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=password\". " +
                "Tests run against real PostgreSQL on purpose - transactions, numeric precision, row " +
                "locks and check constraints cannot be verified against the EF Core InMemory provider.");
        }

        _database = await PostgresTestDatabase.CreateAsync();
        await _database.MigrateAsync();
        _tableNames = await LoadTableNamesAsync();
    }

    public ScmDbContext CreateContext() => _database.CreateContext();

    /// <summary>
    /// Empties every table and restarts identity sequences so ids are predictable per test.
    /// </summary>
    public async Task ResetAsync()
    {
        if (_tableNames.Length == 0)
        {
            return;
        }

        await using var context = CreateContext();
        var quoted = string.Join(", ", _tableNames.Select(t => $"\"{t}\""));

        // Table names come from information_schema on a throwaway test database, never from user
        // input, and identifiers cannot be parameterised in TRUNCATE.
#pragma warning disable EF1002
        await context.Database.ExecuteSqlRawAsync(
            $"TRUNCATE TABLE {quoted} RESTART IDENTITY CASCADE;");
#pragma warning restore EF1002
    }

    private async Task<string[]> LoadTableNamesAsync()
    {
        await using var context = CreateContext();
        var names = new List<string>();

        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type = 'BASE TABLE'
              AND table_name <> '__EFMigrationsHistory';
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return [.. names];
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}

/// <summary>
/// All database tests share one collection so they never run in parallel against the shared schema.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ScmDatabaseCollection : ICollectionFixture<ScmDatabaseFixture>
{
    public const string Name = "scm-database";
}

/// <summary>
/// Base class handling per-test truncation.
/// </summary>
[Collection(ScmDatabaseCollection.Name)]
public abstract class DatabaseTestBase : IAsyncLifetime
{
    protected DatabaseTestBase(ScmDatabaseFixture fixture) => Fixture = fixture;

    protected ScmDatabaseFixture Fixture { get; }

    protected ScmDbContext CreateContext() => Fixture.CreateContext();

    public virtual async Task InitializeAsync() => await Fixture.ResetAsync();

    public virtual Task DisposeAsync() => Task.CompletedTask;
}
