using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SUPPORT.Application.Common.Behaviors;

namespace SUPPORT.Application;

/// <summary>Registers the application layer: MediatR handlers, validators and the validation pipelines.</summary>
public static class DependencyInjection
{
    /// <summary>Adds MediatR + FluentValidation for this assembly.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSupportApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenStreamBehavior(typeof(StreamValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }
}
