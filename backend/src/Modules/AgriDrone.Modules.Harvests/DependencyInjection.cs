using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.Modules.Harvests;

public static class DependencyInjection
{
    public static IServiceCollection AddHarvestsModule(
        this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        return services;
    }
}
