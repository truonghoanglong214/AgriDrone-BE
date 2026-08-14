using AgriDrone.Modules.FieldTasks.Domain.FieldTasks;
using AgriDrone.Modules.FieldTasks.Domain.Updates;
using AgriDrone.Modules.FieldTasks.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.Modules.FieldTasks;

public static class DependencyInjection
{
    public static IServiceCollection AddFieldTasksModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredAgriDroneConnectionString();
        var translator = UpperSnakeCaseNameTranslator.Instance;

        services.AddDbContext<FieldTasksDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .MapEnum<FieldTaskType>("task_type", "system", translator)
                    .MapEnum<FieldTaskPriority>("task_priority", "system", translator)
                    .MapEnum<FieldTaskStatus>("task_status", "system", translator)
                    .MapEnum<FieldTaskResult>("task_result", "system", translator)));

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(mediatR =>
            mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        return services;
    }
}
