using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SUPPORT.Infrastructure;

/// <summary>Registers infrastructure services (persistence, AI clients, knowledge ingestion).</summary>
public static class DependencyInjection
{
    /// <summary>Adds infrastructure services. Empty for now — filled in from phase P1 of the plan.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSupportInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        return services;
    }
}
