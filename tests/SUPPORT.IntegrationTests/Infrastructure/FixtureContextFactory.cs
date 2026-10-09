using Microsoft.EntityFrameworkCore;
using SUPPORT.Infrastructure.Persistence;

namespace SUPPORT.IntegrationTests.Infrastructure;

/// <summary><see cref="IDbContextFactory{TContext}"/> over the test database, for services that need a factory.</summary>
/// <param name="fixture">Database fixture.</param>
public sealed class FixtureContextFactory(PostgresFixture fixture) : IDbContextFactory<SupportDbContext>
{
    /// <inheritdoc />
    public SupportDbContext CreateDbContext() => fixture.CreateContext();
}
