using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace SUPPORT.Application;

/// <summary>Registers the application layer: MediatR handlers and FluentValidation validators.</summary>
public static class DependencyInjection
{
    /// <summary>Adds MediatR + FluentValidation for this assembly.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSupportApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
