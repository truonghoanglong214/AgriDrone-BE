using System.Diagnostics.Metrics;
using AgriDrone.SharedInfrastructure.Authentication;

namespace AgriDrone.Api.Legacy;

public static class LegacyEndpointTelemetry
{
    public const string MeterName = "AgriDrone.Api.LegacyEndpoints";
    public const string AttemptCounterName =
        "legacy_endpoint_attempt_total";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> AttemptCounter =
        Meter.CreateCounter<long>(
            AttemptCounterName,
            unit: "attempts",
            description: "Calls to endpoints retained only for legacy compatibility.");

    public static void RecordAttempt(
        HttpContext context,
        string routeName,
        bool allowedByFeatureFlag)
    {
        ArgumentNullException.ThrowIfNull(context);

        AttemptCounter.Add(
            1,
            new KeyValuePair<string, object?>("route", routeName),
            new KeyValuePair<string, object?>(
                "actor",
                ResolveActorCategory(context.User)),
            new KeyValuePair<string, object?>(
                "outcome",
                allowedByFeatureFlag ? "allowed" : "blocked"));
    }

    public static string ResolveActorCategory(
        System.Security.Claims.ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity?.IsAuthenticated != true)
        {
            return "public";
        }

        var systemRole = principal.FindFirst(
            AgriDroneClaimTypes.SystemRole)?.Value;

        if (!string.IsNullOrWhiteSpace(systemRole))
        {
            return systemRole.Trim().ToLowerInvariant();
        }

        var tenantRole = principal.FindFirst(
            AgriDroneClaimTypes.TenantRole)?.Value;

        return string.IsNullOrWhiteSpace(tenantRole)
            ? "authenticated"
            : tenantRole.Trim().ToLowerInvariant();
    }
}
