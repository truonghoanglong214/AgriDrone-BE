using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using AgriDrone.Modules.Missions.Infrastructure.Integration;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.Modules.Missions.Infrastructure.Queries;
using AgriDrone.Modules.Missions.Infrastructure.Repositories;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.Modules.Missions;

public static class DependencyInjection
{
    public static IServiceCollection AddMissionsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredAgriDroneConnectionString();
        var translator = UpperSnakeCaseNameTranslator.Instance;

        services.AddDbContext<MissionsDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .UseNetTopologySuite()
                    .MapEnum<DroneStatus>("drone_status", "system", translator)
                    .MapEnum<MissionType>("mission_type", "system", translator)
                    .MapEnum<MissionStatus>("mission_status", "system", translator)
                    .MapEnum<ProcessingStatus>("processing_status", "system", translator)
                    .MapEnum<MediaType>("media_type", "system", translator)
                    .MapEnum<MediaStorageStatus>("media_storage_status", "system", translator)
                    .MapEnum<MissionMediaRole>("mission_media_role", "system", translator)
                    .MapEnum<AltitudeReference>("altitude_reference", "system", translator)
                    .MapEnum<AiModelType>("ai_model_type", "system", translator)
                    .MapEnum<AiJobType>("ai_job_type", "system", translator)
                    .MapEnum<AiJobStatus>("ai_job_status", "system", translator)
                    .MapEnum<ThresholdProfileStatus>(
                        "threshold_profile_status",
                        "system",
                        translator)
                    .MapEnum<ObservationReviewStatus>(
                        "observation_review_status",
                        "system",
                        translator)
                    .MapEnum<AuditActorType>(
                        "audit_actor_type",
                        "system",
                        translator)
                    .MapEnum<MatchStrategy>("match_strategy", "system", translator)));
        services.AddScoped<IMissionsUnitOfWork>(
                serviceProvider =>
                    serviceProvider.GetRequiredService<MissionsDbContext>());

        services.AddScoped<
            IDroneRepository,
            DroneRepository>();

        services.AddScoped<
            IDroneQueries,
            DroneQueries>();
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(mediatR =>
            mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        services.AddScoped<
            IIntegrationMessageHandler<ZoneMapPublishedV1>,
            ZoneMapPublishedHandler>();
        services.AddIntegrationConsumer<ZoneMapPublishedProcessor>(
            IntegrationConsumerNames.Be2ZoneMapPublishedV1);

        services.AddScoped<
            IIntegrationMessageHandler<HealthReviewStateChangedV1>,
            HealthReviewStateChangedHandler>();

        services.AddScoped<
            IDroneMissionRepository, DroneMissionRepository>();

        services.AddScoped<
            IMissionQueries, MissionQueries>();

        services.AddIntegrationConsumer<
            HealthReviewStateChangedProcessor>(
            IntegrationConsumerNames
                .Be2HealthReviewStateChangedV1);

        return services;
    }
}
