using Microsoft.EntityFrameworkCore;
using Npgsql;
using SUPPORT.Infrastructure.Persistence;

namespace SUPPORT.IntegrationTests.Infrastructure;

/// <summary>
/// Creates a throwaway database on the docker-compose pgvector server, migrates it with the real Init migration,
/// and drops it when the test collection finishes.
/// </summary>
/// <remarks>
/// Testcontainers would be the usual choice, but this machine's Application Control policy blocks its assembly,
/// so the tests reuse the server from <c>docker compose up -d</c> instead. Each run gets its own database, so the
/// dev database (<c>support</c>) is never touched.
/// Set <c>SUPPORT_TEST_DB</c> to a connection string for that server (any database; the user needs CREATEDB).
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string ConnectionVariable = "SUPPORT_TEST_DB";

    private readonly NpgsqlConnectionStringBuilder _server;
    private readonly string _databaseName = $"support_test_{Guid.NewGuid():N}";

    /// <summary>Reads the server connection string from <c>SUPPORT_TEST_DB</c>.</summary>
    public PostgresFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"{ConnectionVariable} is not set. Start `docker compose up -d` and set it, e.g. " +
                $"{ConnectionVariable}=\"Host=localhost;Port=5433;Username=support;Password=<SUPPORT_PG_PASSWORD>;Database=postgres\"");

        _server = new NpgsqlConnectionStringBuilder(connectionString);
    }

    /// <summary>Connection string of the test database.</summary>
    public string ConnectionString =>
        new NpgsqlConnectionStringBuilder(_server.ConnectionString) { Database = _databaseName }.ConnectionString;

    /// <summary>Creates a fresh context against the test database.</summary>
    /// <returns>A new context; dispose it after use.</returns>
    public SupportDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<SupportDbContext>();
        SupportDbContextOptions.Configure(builder, ConnectionString);
        return new SupportDbContext(builder.Options);
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await ExecuteOnServerAsync($"CREATE DATABASE \"{_databaseName}\"");
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_server.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>Shares one <see cref="PostgresFixture"/> across all database test classes.</summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    /// <summary>Collection name.</summary>
    public const string Name = "postgres";
}
