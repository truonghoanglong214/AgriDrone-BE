using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Diseases;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Domain.Scans;
using AgriDrone.Modules.Plants.Domain.Verifications;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.Modules.Plants;

public static class DependencyInjection
{
    public static IServiceCollection AddPlantsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredAgriDroneConnectionString();
        var translator = UpperSnakeCaseNameTranslator.Instance;

        services.AddDbContext<PlantsDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .UseNetTopologySuite()
                    .MapEnum<PlantLifecycleStatus>(
                        "plant_lifecycle_status",
                        "system",
                        translator)
                    .MapEnum<PositionSource>("position_source", "system", translator)
                    .MapEnum<ConditionType>("condition_type", "system", translator)
                    .MapEnum<PlantChangeType>("plant_change_type", "system", translator)
                    .MapEnum<PlantChangeSource>("plant_change_source", "system", translator)
                    .MapEnum<ReviewStatus>("review_status", "system", translator)
                    .MapEnum<ScanSource>("scan_source", "system", translator)
                    .MapEnum<ScanMediaRole>("scan_media_role", "system", translator)
                    .MapEnum<FindingSource>("finding_source", "system", translator)
                    .MapEnum<VerificationDecision>(
                        "verification_decision",
                        "system",
                        translator)
                    .MapEnum<ConditionReviewDecision>(
                        "condition_review_decision",
                        "system",
                        translator)));

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(mediatR =>
            mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        return services;
    }
}
