using Microsoft.EntityFrameworkCore;

namespace SUPPORT.Infrastructure.Persistence;

/// <summary>Single place that configures the provider, so runtime, design-time and tests build the same model.</summary>
internal static class SupportDbContextOptions
{
    /// <summary>Applies Npgsql + pgvector + snake_case naming to <paramref name="builder"/>.</summary>
    /// <param name="builder">Options builder to configure.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <returns>The same builder for chaining.</returns>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention();
}
