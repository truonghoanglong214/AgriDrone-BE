using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Database.Mapping;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AgriDrone.Database;

public static class DependencyInjection
{
    public static IServiceCollection AddAgriDroneDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetRequiredAgriDroneConnectionString();

        services.AddSingleton(_ =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            PostgreSqlEnumMappings.ConfigureDataSource(dataSourceBuilder);
            return dataSourceBuilder.Build();
        });

        services.AddDbContext<AgriDroneSchemaDbContext>((serviceProvider, options) =>
        {
            var dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();
            AgriDroneSchemaDbContextOptions.Configure(options, dataSource);
        });

        return services;
    }

    public static IServiceCollection AddMappingPublicationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetRequiredAgriDroneConnectionString();
        var translator = UpperSnakeCaseNameTranslator.Instance;

        services.AddDbContext<MappingPublicationDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .UseNetTopologySuite()
                    .MapEnum<GeneralStatus>(
                        "general_status",
                        DbSchemas.System,
                        translator)
                    .MapEnum<MapVersionStatus>(
                        "map_version_status",
                        DbSchemas.System,
                        translator)
                    .MapEnum<PlantLifecycleStatus>(
                        "plant_lifecycle_status",
                        DbSchemas.System,
                        translator)
                    .MapEnum<PositionSource>(
                        "position_source",
                        DbSchemas.System,
                        translator)
                    .MapEnum<PlantChangeType>(
                        "plant_change_type",
                        DbSchemas.System,
                        translator)
                    .MapEnum<PlantChangeSource>(
                        "plant_change_source",
                        DbSchemas.System,
                        translator)
                    .MapEnum<ReviewStatus>(
                        "review_status",
                        DbSchemas.System,
                        translator)
                    .MapEnum<MissionType>(
                        "mission_type",
                        DbSchemas.System,
                        translator)
                    .MapEnum<MissionStatus>(
                        "mission_status",
                        DbSchemas.System,
                        translator)
                    .MapEnum<ProcessingStatus>(
                        "processing_status",
                        DbSchemas.System,
                        translator)
                    .MapEnum<AuditActorType>(
                        "audit_actor_type",
                        DbSchemas.System,
                        translator)));

        services.AddScoped<
            IIntegrationMessageHandler<MappingCandidatesApprovedV1>,
            MappingCandidatesApprovedHandler>();
        services.AddIntegrationConsumer<MappingCandidatesApprovedProcessor>(
            IntegrationConsumerNames.Be1MappingCandidatesApprovedV1);

        return services;
    }
}
