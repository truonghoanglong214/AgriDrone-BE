using AgriDrone.SharedInfrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AgriDrone.SharedInfrastructure.Health;

internal sealed class RabbitMqReadinessHealthCheck(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options) : IHealthCheck
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy(
                "RabbitMQ is disabled by configuration.");
        }

        try
        {
            var connection = await connectionProvider.GetConnectionAsync(
                cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ is connected.")
                : HealthCheckResult.Unhealthy(
                    "RabbitMQ connection is not open.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "RabbitMQ is unavailable.",
                exception);
        }
    }
}
