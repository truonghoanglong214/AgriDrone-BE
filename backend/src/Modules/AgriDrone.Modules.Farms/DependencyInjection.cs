using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.Modules.Farms;

public static class DependencyInjection
{
    public static IServiceCollection AddFarmsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredAgriDroneConnectionString();
        var translator = UpperSnakeCaseNameTranslator.Instance;

        services.AddDbContext<FarmsDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .UseNetTopologySuite()
                    .MapEnum<GeneralStatus>("general_status", "system", translator)
                    .MapEnum<MapVersionStatus>("map_version_status", "system", translator)));

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(mediatR =>
            mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        return services;
    }
}
