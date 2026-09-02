using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.Modules.Farms.Infrastructure.Queries;
using AgriDrone.Modules.Farms.Infrastructure.Repositories;
using AgriDrone.SharedInfrastructure.Auditing;
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
                    .MapEnum<MapVersionStatus>("map_version_status", "system", translator)
                    .MapEnum<AuditActorType>("audit_actor_type", "system", translator)));

        services.AddScoped<IFarmUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<FarmsDbContext>());
        services.AddScoped<IFarmRepository, FarmRepository>();
        services.AddScoped<IFarmZoneRepository, FarmZoneRepository>();
        services.AddScoped<IFarmQueries, FarmQueries>();
        services.AddScoped<
            IMissionPlanningReferenceQuery,
            MissionPlanningReferenceQuery>();

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(mediatR =>
            mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        return services;
    }
}
