using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace api_scm.Tests.Infrastructure;

/// <summary>
/// Creates a throwaway PostgreSQL database for a test run and drops it on dispose.
/// </summary>
/// <remarks>
/// The implementation plan called for Testcontainers.PostgreSql. Docker is not installed on this
/// machine, so we target the locally installed PostgreSQL service instead. The intent is preserved:
/// tests run against real PostgreSQL, so transactions, <c>numeric</c> precision, row locks and
/// check constraints all behave the way they will in production. EF Core's InMemory provider
/// cannot honour any of those, which is why it is deliberately not used.
/// <para>
/// Override the admin connection with the <c>SCM_TEST_PG</c> environment variable when the local
/// credentials differ from the developer default.
/// </para>
/// </remarks>
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private const string DefaultAdminConnection =
        "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=password";

    private readonly string _adminConnectionString;
    private readonly string _databaseName;
    private bool _created;

    private PostgresTestDatabase(string adminConnectionString, string databaseName)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = databaseName,
            // Test assertions read rows written by a different context instance; pooling is fine,
            // but we keep the pool small so a run does not exhaust local server connections.
            MaxPoolSize = 10
        };
        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }

    public static string AdminConnectionString =>
        Environment.GetEnvironmentVariable("SCM_TEST_PG") is { Length: > 0 } fromEnv
            ? fromEnv
            : DefaultAdminConnection;

    /// <summary>
    /// True when a PostgreSQL server is reachable. Tests skip rather than fail when it is not,
    /// so the suite stays usable on machines without a local database.
    /// </summary>
    public static async Task<bool> IsServerAvailableAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(AdminConnectionString);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await connection.OpenAsync(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<PostgresTestDatabase> CreateAsync(string namePrefix = "scm_test")
    {
        var databaseName = $"{namePrefix}_{Guid.NewGuid():N}";
        var database = new PostgresTestDatabase(AdminConnectionString, databaseName);
        await database.CreateDatabaseAsync();
        return database;
    }

    private async Task CreateDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        // Identifier is a generated GUID-suffixed name, not user input, but quote it regardless.
        command.CommandText = $"CREATE DATABASE \"{_databaseName}\" TEMPLATE template0;";
        await command.ExecuteNonQueryAsync();
        _created = true;
    }

    /// <summary>
    /// Applies the EF Core migration chain. Using migrations rather than EnsureCreated means every
    /// schema change added by this rebuild is exercised end to end before it reaches a real database.
    /// </summary>
    public async Task MigrateAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public ScmDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ScmDbContext>()
            .UseNpgsql(ConnectionString)
            .EnableSensitiveDataLogging()
            .Options;
        return new ScmDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_created)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE);";
        await command.ExecuteNonQueryAsync();
        _created = false;
    }
}
