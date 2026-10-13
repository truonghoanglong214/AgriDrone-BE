using AgriDrone.Modules.Plants.Infrastructure.Initialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgriDrone.Modules.Plants.Infrastructure.Health;

internal sealed class HealthLevelSeedReadinessHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var validator = scope.ServiceProvider
                .GetRequiredService<HealthLevelSeedValidator>();
            var result = await validator.ValidateAsync(cancellationToken);

            return result.IsValid
                ? HealthCheckResult.Healthy(
                    "Core health-level seeds are valid.")
                : HealthCheckResult.Unhealthy(result.ErrorMessage);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Core health-level seeds could not be validated.",
                exception);
        }
    }
}
