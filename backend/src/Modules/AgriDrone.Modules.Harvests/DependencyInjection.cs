using AgriDrone.Modules.Harvests.Application.Abstractions.Persistence;
using AgriDrone.Modules.Harvests.Application.Abstractions.Queries;
using AgriDrone.Modules.Harvests.Domain.HarvestBatches;
using AgriDrone.Modules.Harvests.Domain.PlantHarvests;
using AgriDrone.Modules.Harvests.Domain.Quality;
using AgriDrone.Modules.Harvests.Domain.Seasons;
using AgriDrone.Modules.Harvests.Infrastructure.Persistence;
using AgriDrone.Modules.Harvests.Infrastructure.Queries;
using AgriDrone.Modules.Harvests.Infrastructure.Repositories;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.Modules.Harvests;

public static class DependencyInjection
{
    public static IServiceCollection AddHarvestsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredAgriDroneConnectionString();
        var translator = UpperSnakeCaseNameTranslator.Instance;

        services.AddDbContext<HarvestsDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .MapEnum<HarvestBatchStatus>("harvest_batch_status", "system", translator)
                    .MapEnum<HarvestRecordSource>("harvest_record_source", "system", translator)
                    .MapEnum<SeasonStatus>("season_status", "system", translator)
                    .MapEnum<AuditActorType>("audit_actor_type", "system", translator)));

        services.AddScoped<IHarvestsUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<HarvestsDbContext>());

        services.AddScoped<IHarvestQualityGradeRepository, HarvestQualityGradeRepository>();
        services.AddScoped<IHarvestQualityGradeQueries, HarvestQualityGradeQueries>();

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(mediatR =>
            mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        return services;
    }
}
