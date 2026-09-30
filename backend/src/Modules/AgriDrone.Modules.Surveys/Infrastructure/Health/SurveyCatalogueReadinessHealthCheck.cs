using AgriDrone.Modules.Surveys.Infrastructure.Initialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgriDrone.Modules.Surveys.Infrastructure.Health;

internal sealed class SurveyCatalogueReadinessHealthCheck(
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
                .GetRequiredService<SurveyCatalogueSeedValidator>();
            var result = await validator.ValidateAsync(cancellationToken);

            return result.IsValid
                ? HealthCheckResult.Healthy(
                    "Survey catalogue storage and required seeds are ready.")
                : HealthCheckResult.Unhealthy(result.ErrorMessage);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Survey catalogue storage could not be validated.",
                exception);
        }
    }
}
