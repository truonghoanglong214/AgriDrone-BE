using AgriDrone.Database.Mapping;
using AgriDrone.Database.Surveys;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.Modules.Surveys.Application.Abstractions.Appointments;
using AgriDrone.Modules.Surveys.Application.Abstractions.BoundaryCapture;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        services.AddScoped<ISurveyOrderOperationalContextV3Query,
            SurveyOrderOperationalContextV3Query>();
        services.AddScoped<ISurveyOrderOperationalContextV4Query,
            SurveyOrderOperationalContextV4Query>();
        services.AddScoped<ISystemManagerSurveyOrderQueries,
            SystemManagerSurveyOrderQueries>();
        services.AddScoped<IV3FollowUpBaselineQuery,
            V3FollowUpBaselineQuery>();
        services.AddScoped<V3FollowUpBaselineEligibilityReader>();
        services.TryAddScoped<IV3BaselineDeliveryProofReader,
            UnavailableV3BaselineDeliveryProofReader>();

        return services;
    }

    public static IServiceCollection AddMappingPublicationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration.GetRequiredAgriDroneConnectionString();

        services.AddDbContext<MappingPublicationDbContext>(
            (serviceProvider, options) =>
            {
                var dataSource =
                    serviceProvider.GetRequiredService<NpgsqlDataSource>();
                options.UseNpgsql(
                    dataSource,
                    npgsql =>
                    {
                        npgsql.UseNetTopologySuite();
                        PostgreSqlEnumMappings.ConfigureOptions(npgsql);
                    });
            });

        services.AddScoped<IMappingPublicationUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<MappingPublicationDbContext>());

        services.AddScoped<
            IIntegrationMessageHandler<MappingCandidatesApprovedV1>,
            MappingCandidatesApprovedHandler>();
        services.AddIntegrationConsumer<MappingCandidatesApprovedProcessor>(
            IntegrationConsumerNames.Be1MappingCandidatesApprovedV1);

        services.AddScoped<
            IIntegrationMessageHandler<BaselineMappingCandidatesReadyV3>,
            BaselineMappingCandidatesReadyV3Handler>();
        services.AddIntegrationConsumer<BaselineMappingCandidatesReadyV3Processor>(
            IntegrationConsumerNames.Be1BaselineMappingCandidatesReadyV3);

        services.AddScoped<IFarmBaseMapPublicationOrchestrator, FarmBaseMapPublicationOrchestrator>();

        return services;
    }

    public static IServiceCollection AddSurveyResultPublicationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration.GetRequiredAgriDroneConnectionString();

        services.AddDbContext<SurveyResultPublicationDbContext>(
            (serviceProvider, options) =>
            {
                var dataSource =
                    serviceProvider.GetRequiredService<NpgsqlDataSource>();
                options.UseNpgsql(
                    dataSource,
                    npgsql =>
                    {
                        npgsql.UseNetTopologySuite();
                        PostgreSqlEnumMappings.ConfigureOptions(npgsql);
                    });
            });

        services.AddScoped<ISurveyResultPublicationUnitOfWork>(
            serviceProvider => serviceProvider.GetRequiredService<
                SurveyResultPublicationDbContext>());

        services.AddScoped<AgriDrone.IntegrationContracts.Health.IPlantHealthPendingImport, Health.PlantHealthPendingImport>();
        services.AddScoped<IIntegrationMessageHandler<AgriDrone.IntegrationContracts.Health.PlantHealthAnalysisReadyV4>, Health.PlantHealthAnalysisReadyV4Handler>();
        services.AddIntegrationConsumer<Health.PlantHealthAnalysisReadyV4Processor>(IntegrationConsumerNames.Be1PlantHealthAnalysisReadyV4);
        services.AddScoped<Health.PlantHealthReviewService>();
        services.AddScoped<AgriDrone.IntegrationContracts.Health.IPlantHealthPublicationQuery, Health.PlantHealthPublicationQuery>();
        services.AddScoped<AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.PublishPlantHealthSurveyResult.IPlantHealthPublishedResultReader>(sp => sp.GetRequiredService<Health.PlantHealthReviewService>());
        services.AddScoped<IIntegrationMessageHandler<AgriDrone.IntegrationContracts.Health.PlantHealthPublicationNotificationRequestedV1>, Health.PlantHealthPublicationNotificationHandler>();
        services.AddIntegrationConsumer<Health.PlantHealthPublicationNotificationProcessor>(IntegrationConsumerNames.PlantHealthPublicationNotificationV1);
        services.AddScoped<AgriDrone.Modules.Surveys.Application.Abstractions.HealthReview.IPlantHealthReviewService>(sp => sp.GetRequiredService<Health.PlantHealthReviewService>());
        services.AddScoped<AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.PublishPlantHealthSurveyResult.IPlantHealthResultPublisher>(sp => sp.GetRequiredService<Health.PlantHealthReviewService>());
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddScoped<AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.PublishPlantHealthSurveyResult.IPlantHealthPublicationCheckpoint, Health.PlantHealthPublicationCheckpoint>(services);
        services.AddScoped<AgriDrone.Modules.Surveys.Application.Abstractions.HealthReview.IPlantHealthReviewAuthorizationGuard, Health.PlantHealthReviewAuthorizationGuard>();

        return services;
    }

    public static IServiceCollection AddSurveyApprovalPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration.GetRequiredAgriDroneConnectionString();

        services.TryAddScoped<ISurveyApprovalFailureInjector,
            SurveyApprovalFailureInjector>();

        services.AddDbContext<SurveyApprovalDbContext>(
            (serviceProvider, options) =>
            {
                var dataSource =
                    serviceProvider.GetRequiredService<NpgsqlDataSource>();
                options.UseNpgsql(
                    dataSource,
                    npgsql =>
                    {
                        npgsql.UseNetTopologySuite();
                        PostgreSqlEnumMappings
                            .ConfigureSurveyApprovalOptions(npgsql);
                    });
            });

        services.AddScoped<ISurveyApprovalUnitOfWork,
            SurveyApprovalUnitOfWork>();
        services.AddScoped<ISurveyApprovalRequestRepository,
            SurveyApprovalRequestRepository>();
        services.AddScoped<ISurveyApprovalIdempotencyRepository,
            SurveyApprovalIdempotencyRepository>();
        services.AddScoped<ISurveyApprovalReferenceQueries,
            SurveyApprovalReferenceQueries>();
        services.AddScoped<ISurveyApprovalProvisioningPort,
            SurveyApprovalProvisioningPort>();
        services.AddScoped<ISurveyApprovalOutbox, SurveyApprovalOutbox>();
        services.AddScoped<ISurveyOrderRepository,
            SurveyApprovalOrderRepository>();
        services.AddScoped<ISurveyCommercialSnapshotResolver,
            SurveyCommercialSnapshotResolver>();
        return services;
    }

    public static IServiceCollection AddSurveyBoundaryVerificationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration.GetRequiredAgriDroneConnectionString();

        services.AddDbContext<SurveyBoundaryVerificationDbContext>(
            (serviceProvider, options) =>
            {
                var dataSource =
                    serviceProvider.GetRequiredService<NpgsqlDataSource>();
                options.UseNpgsql(
                    dataSource,
                    npgsql =>
                    {
                        npgsql.UseNetTopologySuite();
                        PostgreSqlEnumMappings.ConfigureOptions(npgsql);
                    });
            });

        services.AddScoped<ISurveyBoundaryVerificationUnitOfWork>(
            serviceProvider => serviceProvider.GetRequiredService<
                SurveyBoundaryVerificationDbContext>());
        services.AddScoped<ISurveyOrderBoundaryRepository,
            SurveyOrderBoundaryRepository>();
        services.AddScoped<ISurveyOrderBoundaryQueries,
            SurveyOrderBoundaryQueries>();
        services.AddScoped<ISurveyBoundaryVerificationOutbox,
            SurveyBoundaryVerificationOutbox>();
        services.AddScoped<IBoundaryCaptureRepository,
            BoundaryCaptureRepository>();

        return services;
    }

    public static IServiceCollection AddSurveyAppointmentSchedulingPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration.GetRequiredAgriDroneConnectionString();

        services.AddDbContext<SurveyAppointmentSchedulingDbContext>(
            (serviceProvider, options) =>
            {
                var dataSource =
                    serviceProvider.GetRequiredService<NpgsqlDataSource>();
                options.UseNpgsql(
                    dataSource,
                    npgsql =>
                    {
                        npgsql.UseNetTopologySuite();
                        PostgreSqlEnumMappings.ConfigureOptions(npgsql);
                    });
            });

        services.AddScoped<ISurveyAppointmentSchedulingUnitOfWork>(
            serviceProvider => serviceProvider.GetRequiredService<
                SurveyAppointmentSchedulingDbContext>());
        services.AddScoped<ISurveyAppointmentSchedulingRepository,
            SurveyAppointmentSchedulingRepository>();
        services.AddScoped<ISurveyAppointmentSchedulingOutbox,
            SurveyAppointmentSchedulingOutbox>();
        services.AddScoped<ISurveyOrderReadinessStateQueries,
            SurveyOrderReadinessStateQueries>();

        return services;
    }

    public static IServiceCollection AddSurveyPricingConfirmationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration.GetRequiredAgriDroneConnectionString();

        services.AddDbContext<SurveyPricingConfirmationDbContext>(
            (serviceProvider, options) =>
            {
                var dataSource =
                    serviceProvider.GetRequiredService<NpgsqlDataSource>();
                options.UseNpgsql(
                    dataSource,
                    npgsql =>
                    {
                        npgsql.UseNetTopologySuite();
                        PostgreSqlEnumMappings.ConfigureOptions(npgsql);
                    });
            });

        services.AddScoped<ISurveyPricingConfirmationUnitOfWork>(
            serviceProvider => serviceProvider.GetRequiredService<
                SurveyPricingConfirmationDbContext>());
        services.AddScoped<ISurveyPricingConfirmationRepository,
            SurveyPricingConfirmationRepository>();
        services.AddScoped<ISurveyPricingConfirmationOutbox,
            SurveyPricingConfirmationOutbox>();

        return services;
    }

    public static IServiceCollection AddSurveyPaymentProcessingPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration.GetRequiredAgriDroneConnectionString();

        services.AddDbContext<SurveyPaymentProcessingDbContext>(
            (serviceProvider, options) =>
            {
                var dataSource =
                    serviceProvider.GetRequiredService<NpgsqlDataSource>();
                options.UseNpgsql(
                    dataSource,
                    npgsql =>
                    {
                        npgsql.UseNetTopologySuite();
                        PostgreSqlEnumMappings.ConfigureOptions(npgsql);
                    });
            });

        services.AddScoped<ISurveyPaymentProcessingUnitOfWork>(
            serviceProvider => serviceProvider.GetRequiredService<
                SurveyPaymentProcessingDbContext>());
        services.AddScoped<ISurveyPaymentProcessingRepository,
            SurveyPaymentProcessingRepository>();
        services.AddScoped<ISurveyPaymentProcessingOutbox,
            SurveyPaymentProcessingOutbox>();

        return services;
    }
}
