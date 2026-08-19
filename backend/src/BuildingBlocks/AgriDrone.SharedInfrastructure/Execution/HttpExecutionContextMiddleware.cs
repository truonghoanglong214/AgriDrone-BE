using AgriDrone.SharedInfrastructure.Authentication;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Extensions.Logging;

namespace AgriDrone.SharedInfrastructure.Execution;

public sealed class HttpExecutionContextMiddleware(RequestDelegate next)
{
    internal const string CorrelationIdHeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(
        HttpContext httpContext,
        IExecutionContextInitializer initializer,
        ILogger<HttpExecutionContextMiddleware> logger)
    {
        var correlationId = GetOrCreateCorrelationId(httpContext);
        var snapshot = ExecutionContextSnapshot.ForHttp(
            GetGuidClaim(httpContext, AgriDroneClaimTypes.TenantId),
            GetGuidClaim(httpContext, JwtRegisteredClaimNames.Sub),
            correlationId);

        httpContext.TraceIdentifier = correlationId.ToString("D");
        httpContext.Response.Headers[CorrelationIdHeaderName] =
            httpContext.TraceIdentifier;

        using var contextLease = initializer.Begin(snapshot);
        using var loggingScope = logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["ExecutionSource"] = snapshot.Source.ToString(),
                ["TenantId"] = snapshot.TenantId,
                ["ActorId"] = snapshot.ActorId,
                ["CorrelationId"] = snapshot.CorrelationId
            });

        await next(httpContext);
    }

    private static Guid GetOrCreateCorrelationId(HttpContext httpContext)
    {
        var rawCorrelationId =
            httpContext.Request.Headers[CorrelationIdHeaderName].ToString();

        return Guid.TryParse(rawCorrelationId, out var correlationId) &&
               correlationId != Guid.Empty
            ? correlationId
            : Guid.NewGuid();
    }

    private static Guid? GetGuidClaim(
        HttpContext httpContext,
        string claimType)
    {
        var value = httpContext.User.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : null;
    }
}
