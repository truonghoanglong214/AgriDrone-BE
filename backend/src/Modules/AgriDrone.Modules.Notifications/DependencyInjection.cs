using AgriDrone.Modules.Notifications.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.Modules.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredAgriDroneConnectionString();

        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(connectionString));

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(mediatR =>
            mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        return services;
    }
}
