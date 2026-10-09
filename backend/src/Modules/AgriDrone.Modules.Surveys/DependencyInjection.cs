using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Health;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence.Repositories;
using AgriDrone.Modules.Surveys.Infrastructure.Queries;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddScoped<IHarvestReadinessCriterionRepository, HarvestReadinessCriterionRepository>();
        services.AddScoped<ISurveyCatalogueQueries, SurveyCatalogueQueries>();
        services.AddScoped<IHarvestReadinessCriterionQueries, HarvestReadinessCriterionQueries>();

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
