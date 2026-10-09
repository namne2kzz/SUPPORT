using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SUPPORT.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the context without booting the API. Reads <c>SUPPORT_DB</c> if set, otherwise
/// points at the local docker-compose database (no credentials needed just to scaffold a migration).
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SupportDbContext>
{
    /// <summary>Creates the context for design-time tooling.</summary>
    /// <param name="args">Tool arguments (unused).</param>
    /// <returns>A configured context.</returns>
    public SupportDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SUPPORT_DB")
            ?? "Host=localhost;Port=5433;Database=support;Username=support";

        var builder = new DbContextOptionsBuilder<SupportDbContext>();
        SupportDbContextOptions.Configure(builder, connectionString);
        return new SupportDbContext(builder.Options);
    }
}
