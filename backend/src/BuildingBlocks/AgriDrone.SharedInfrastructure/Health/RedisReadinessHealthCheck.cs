using AgriDrone.SharedInfrastructure.Caching;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AgriDrone.SharedInfrastructure.Health;

internal sealed class RedisReadinessHealthCheck(
    RedisConnectionProvider connectionProvider,
    IOptions<RedisCacheOptions> options) : IHealthCheck
{
    private readonly RedisCacheOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy(
                "Redis is disabled by configuration.");
        }

        try
        {
            var connection = await connectionProvider.GetConnectionAsync(
                cancellationToken);
            var latency = await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy(
                $"Redis responded in {latency.TotalMilliseconds:F0} ms.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Redis is unavailable.",
                exception);
        }
    }
}
