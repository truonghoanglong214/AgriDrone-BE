using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgriDrone.SharedInfrastructure.Health;

internal sealed class SelfHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Healthy("Process is running."));
}
