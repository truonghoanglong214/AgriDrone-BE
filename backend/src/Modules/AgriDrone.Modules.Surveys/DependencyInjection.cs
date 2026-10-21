using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Missions;
using AgriDrone.Modules.Surveys.Application.Abstractions.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Abstractions.Requests;
using AgriDrone.Modules.Surveys.Application.Services;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.Common;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;
using AgriDrone.Modules.Surveys.Application.Abstractions.Adjustments;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Approvals;
using AgriDrone.Modules.Surveys.Infrastructure.Health;
using AgriDrone.Modules.Surveys.Infrastructure.Messaging;
using AgriDrone.Modules.Surveys.Infrastructure.Messaging.Consumers;
using AgriDrone.Modules.Surveys.Infrastructure.Payments;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;
using AgriDrone.Modules.Surveys.Infrastructure.Queries;
using AgriDrone.Modules.Surveys.Infrastructure.Requests;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Surveys;

public static class DependencyInjection
{
    public static IServiceCollection AddSurveysModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetRequiredAgriDroneConnectionString();
        var translator = UpperSnakeCaseNameTranslator.Instance;

        services.AddDbContext<SurveysDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .UseNetTopologySuite()
                    .MapEnum<SurveyServiceType>(
                        "survey_service_type",
                        "system",
                        translator)
                    .MapEnum<SurveyServiceStatus>(
                        "survey_service_status",
                        "system",
                        translator)
                    .MapEnum<SurveyRequestKind>(
                        "survey_request_kind",
                        "system",
                        translator)
                    .MapEnum<SurveyRequestStatus>(
                        "survey_request_status",
                        "system",
                        translator)
                    .MapEnum<SurveyReviewDecision>(
                        "survey_review_decision",
                        "system",
                        translator)
                    .MapEnum<SurveyOrderStatus>(
                        "survey_order_status",
                        "system",
                        translator)
                    .MapEnum<SurveyPoleCountSource>(
                        "survey_pole_count_source",
                        "system",
                        translator)
                    .MapEnum<SurveyAppointmentPurpose>(
                        "survey_appointment_purpose",
                        "system",
                        translator)
                    .MapEnum<SurveyAppointmentStatus>(
                        "survey_appointment_status",
                        "system",
                        translator)
                    .MapEnum<SurveyPaymentStatus>(
                        "survey_payment_status",
                        "system",
                        translator)
                    .MapEnum<PriceAdjustmentStatus>(
                        "price_adjustment_status",
                        "system",
                        translator)
                    .MapEnum<SurveyCaptureAuthorizationType>(
                        "survey_capture_authorization_type",
                        "system",
                        translator)
                    .MapEnum<SurveyCaptureAuthorizationStatus>(
                        "survey_capture_authorization_status",
                        "system",
                        translator)
                    .MapEnum<SurveyResultStatus>(
                        "survey_result_status",
                        "system",
                        translator)
                    .MapEnum<HarvestReadinessReviewStatus>(
                        "harvest_readiness_review_status",
                        "system",
                        translator)
                    .MapEnum<HarvestReadinessCriterionStatus>(
                        "harvest_readiness_criterion_status",
                        "system",
                        translator)
                    .MapEnum<HarvestReadinessGranularity>(
                        "harvest_readiness_granularity",
                        "system",
                        translator)
                    .MapEnum<AuditActorType>(
                        "audit_actor_type",
                        "system",
                        translator)));

        services.AddScoped<ISurveysUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<SurveysDbContext>());
        services.AddScoped<ISurveyServiceRepository, SurveyServiceRepository>();
        services.AddScoped<ISurveyCaptureAuthorizationRepository,
            SurveyCaptureAuthorizationRepository>();
        services.AddScoped<IScopeAdjustmentRepository, ScopeAdjustmentRepository>();
        services.AddScoped<IAdjustmentSettlementRepository, AdjustmentSettlementRepository>();
        services.AddScoped<IExtraFlightRequestRepository, ExtraFlightRequestRepository>();
        services.AddScoped<ISurveyRequestRepository, SurveyRequestRepository>();
        services.AddScoped<ISurveyIntegrationOutbox, SurveyIntegrationOutbox>();
        services.AddScoped<ISurveyCatalogueQueries, SurveyCatalogueQueries>();
        services.AddScoped<ISurveyRequestQueries, SurveyRequestQueries>();
        services.AddScoped<ISurveyOrderArchiveReferenceQuery, SurveyOrderArchiveReferenceQuery>();
        services.AddScoped<ISurveyRequestIdempotencyResolver, SurveyRequestIdempotencyResolver>();
        services.AddOptions<SurveyPaymentProviderOptions>()
            .Bind(configuration.GetSection(SurveyPaymentProviderOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SurveyPaymentProviderOptions>,
            SurveyPaymentProviderOptionsValidator>();
        services.AddScoped<ISurveyPaymentGateway, SurveyPaymentGateway>();
        services.AddScoped<ISurveyPaymentCallbackVerifier,
            SurveyPaymentCallbackVerifier>();
        services.AddScoped<ISurveyOrderReadinessService,
            SurveyOrderReadinessService>();
        services.AddSingleton<ISurveyRequestNumberGenerator, SurveyRequestNumberGenerator>();
        services.AddSingleton<
            ISurveyApprovalNumberGenerator,
            SurveyApprovalNumberGenerator>();
        services.AddScoped<ISurveyApprovalPrerequisiteResolver,
            SurveyApprovalPrerequisiteResolver>();
        services.AddScoped<ISurveyRequestKindApprovalOrchestrator,
            NewCustomerApprovalOrchestrator>();
        services.AddScoped<ISurveyRequestKindApprovalOrchestrator,
            ExistingTenantNewFarmApprovalOrchestrator>();
        services.AddScoped<ISurveyRequestKindApprovalOrchestrator,
            ExistingFarmSurveyApprovalOrchestrator>();

        // Integration Event Consumers for Lifecycle Synchronization
        services.AddScoped<
            IIntegrationMessageHandler<BaselineMissionStartedV1>,
            BaselineMissionStartedHandler>();
        services.AddIntegrationConsumer<BaselineMissionStartedProcessor>(
            IntegrationConsumerNames.Be1BaselineMissionStartedV1);

        services.AddScoped<
            IIntegrationMessageHandler<BaselineOrthophotoCompletedV1>,
            BaselineOrthophotoCompletedHandler>();
        services.AddIntegrationConsumer<BaselineOrthophotoCompletedProcessor>(
            IntegrationConsumerNames.Be1BaselineOrthophotoCompletedV1);

        services.AddScoped<
            IIntegrationMessageHandler<BaselineMappingCandidatesReadyV3>,
            BaselineMappingCandidatesReadyV3Handler>();
        services.AddIntegrationConsumer<BaselineMappingCandidatesReadyV3Processor>(
            IntegrationConsumerNames.Be1BaselineMappingCandidatesReadyV3);

        services.TryAddSingleton(TimeProvider.System);

        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(mediatR =>
            mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        services.AddHealthChecks()
            .AddCheck<SurveysDatabaseReadinessHealthCheck>(
                "surveys-database",
                tags: ["ready"]);

        return services;
    }
}
