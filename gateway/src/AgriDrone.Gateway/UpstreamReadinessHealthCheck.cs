using Microsoft.Extensions.Diagnostics.HealthChecks;

internal sealed class UpstreamReadinessHealthCheck(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var endpoints = configuration
            .GetSection("Gateway:ReadinessEndpoints")
            .Get<string[]>() ?? [];

        if (endpoints.Length == 0)
        {
            return HealthCheckResult.Unhealthy(
                "No Gateway:ReadinessEndpoints are configured.");
        }

        var client = httpClientFactory.CreateClient("readiness");

        foreach (var endpoint in endpoints)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            {
                return HealthCheckResult.Unhealthy(
                    $"The readiness endpoint '{endpoint}' is not an absolute URI.");
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Unhealthy(
                        $"Upstream '{uri.Host}' returned HTTP {(int)response.StatusCode}.");
                }
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException)
            {
                return HealthCheckResult.Unhealthy(
                    $"Upstream '{uri.Host}' is unavailable.", exception);
            }
        }

        return HealthCheckResult.Healthy("All upstream services are ready.");
    }
}
