using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgriDrone.Modules.Surveys.Infrastructure.Health;

internal sealed class SurveysDatabaseReadinessHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider
                .GetRequiredService<SurveysDbContext>();
            var canConnect = await dbContext.Database
                .CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy(
                    "Surveys database boundary is reachable.")
                : HealthCheckResult.Unhealthy(
                    "Surveys database boundary is unavailable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Surveys database boundary could not be validated.",
                exception);
        }
    }
}
