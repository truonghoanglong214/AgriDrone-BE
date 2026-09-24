using Microsoft.Extensions.Options;

namespace AgriDrone.Api.Legacy;

public sealed partial class LegacyEndpointGateMiddleware(
    RequestDelegate next,
    IOptions<LegacyFeaturesOptions> options,
    ILogger<LegacyEndpointGateMiddleware> logger)
{
    public const string DisabledErrorCode = "LegacyFlow.Disabled";

    private readonly LegacyFeaturesOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var legacyEndpoint = context.GetEndpoint()?
            .Metadata.GetMetadata<LegacyEndpointAttribute>();

        if (legacyEndpoint is null)
        {
            await next(context);
            return;
        }

        var enabled = _options.EnableDeprecatedEndpoints;
        var actor = LegacyEndpointTelemetry.ResolveActorCategory(context.User);

        LegacyEndpointTelemetry.RecordAttempt(
            context,
            legacyEndpoint.RouteName,
            enabled);

        LogLegacyEndpointAttempt(
            logger,
            legacyEndpoint.RouteName,
            actor,
            enabled,
            context.TraceIdentifier);

        if (enabled)
        {
            await next(context);
            return;
        }

        await Results.Problem(
                statusCode: StatusCodes.Status410Gone,
                title: "Legacy flow disabled",
                detail:
                    "This endpoint is no longer available in the Be-Plan workflow. " +
                    legacyEndpoint.Replacement,
                instance: context.Request.Path,
                extensions: new Dictionary<string, object?>
                {
                    ["errorCode"] = DisabledErrorCode,
                    ["legacyRoute"] = legacyEndpoint.RouteName,
                    ["replacement"] = legacyEndpoint.Replacement,
                    ["traceId"] = context.TraceIdentifier
                })
            .ExecuteAsync(context);
    }

    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Warning,
        Message =
            "Legacy endpoint attempt. Route={Route}; Actor={Actor}; " +
            "AllowedByFeatureFlag={AllowedByFeatureFlag}; TraceId={TraceId}")]
    private static partial void LogLegacyEndpointAttempt(
        ILogger logger,
        string route,
        string actor,
        bool allowedByFeatureFlag,
        string traceId);
}
