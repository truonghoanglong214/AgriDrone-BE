using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.SharedInfrastructure.Health;

public static class DependencyInjection
{
    public static IServiceCollection AddAgriDroneHealthChecks(
        this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<SelfHealthCheck>(
                "self",
                tags: ["live"])
            .AddCheck<PostgreSqlReadinessHealthCheck>(
                "postgresql",
                tags: ["ready"])
            .AddCheck<RabbitMqReadinessHealthCheck>(
                "rabbitmq",
                tags: ["ready"])
            .AddCheck<RedisReadinessHealthCheck>(
                "redis",
                tags: ["ready"]);
        return services;
    }
}
