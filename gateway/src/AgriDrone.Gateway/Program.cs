using System.Diagnostics;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Yarp.ReverseProxy.Transforms;

const string FrontendCorsPolicy = "frontend";
const string CorrelationIdHeader = "X-Correlation-ID";

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var identityUpstream = builder.Configuration
    .GetValue("Gateway:IdentityUpstream", "be2")
    .Trim()
    .ToLowerInvariant();

if (identityUpstream is not ("be1" or "be2"))
{
    throw new InvalidOperationException(
        "Gateway:IdentityUpstream must be either 'be1' or 'be2'.");
}

foreach (var routeId in new[]
         {
             "identity-login",
             "identity-forgot-password",
             "identity-reset-password"
         })
{
    builder.Configuration[$"ReverseProxy:Routes:{routeId}:ClusterId"] =
        identityUpstream;
}

var allowedOrigins = builder.Configuration
    .GetSection("Gateway:AllowedOrigins")
    .Get<string[]>() ?? [];

if (allowedOrigins.Length == 0 || allowedOrigins.Any(string.IsNullOrWhiteSpace))
{
    throw new InvalidOperationException(
        "At least one non-empty Gateway:AllowedOrigins entry is required.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://httpstatuses.com/429",
            title = "Too many requests.",
            status = StatusCodes.Status429TooManyRequests,
            detail = "The request rate limit was exceeded. Try again later."
        }, cancellationToken);
    };

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            GetClientPartition(httpContext),
            _ => new SlidingWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                SegmentsPerWindow = 6,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("api", httpContext =>
        RateLimitPartition.GetTokenBucketLimiter(
            GetClientPartition(httpContext),
            _ => new TokenBucketRateLimiterOptions
            {
                AutoReplenishment = true,
                TokenLimit = 300,
                TokensPerPeriod = 300,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    options.AddFixedWindowLimiter("docs", limiter =>
    {
        limiter.AutoReplenishment = true;
        limiter.PermitLimit = 60;
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformContext =>
    {
        transformContext.AddResponseHeaderRemove("Server");
        transformContext.AddResponseHeaderRemove("X-Powered-By");
    });

builder.Services.AddHttpClient("readiness", client =>
    client.Timeout = TimeSpan.FromSeconds(3));
builder.Services.AddHealthChecks()
    .AddCheck<UpstreamReadinessHealthCheck>(
        "upstreams",
        tags: ["ready"]);

var app = builder.Build();
var docsEnabled = app.Configuration.GetValue("Gateway:DocsEnabled", true);

app.Use(async (context, next) =>
{
    var suppliedCorrelationId = context.Request.Headers[CorrelationIdHeader].ToString();
    var correlationId = !string.IsNullOrWhiteSpace(suppliedCorrelationId) &&
                        suppliedCorrelationId.Length <= 128
        ? suppliedCorrelationId
        : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

    context.Request.Headers[CorrelationIdHeader] = correlationId;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers[CorrelationIdHeader] = correlationId;
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Task.CompletedTask;
    });

    await next();
});

app.Use(async (context, next) =>
{
    if (!docsEnabled && context.Request.Path.StartsWithSegments("/docs"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

app.UseRouting();
app.UseCors();
app.UseRateLimiter();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");

if (docsEnabled)
{
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "docs";
        options.DocumentTitle = "AgriDrone API documentation";
        options.DisplayRequestDuration();
        options.EnablePersistAuthorization();
        options.SwaggerEndpoint("/docs/be1/openapi.json", "AgriDrone BE1 Java");
        options.SwaggerEndpoint("/docs/be2/openapi.json", "AgriDrone BE2 .NET");
    });

    app.MapGet("/", () => Results.Redirect("/docs"));
}
else
{
    app.MapGet("/", () => Results.Ok(new { service = "AgriDrone.Gateway" }));
}

app.MapReverseProxy();

app.Run();

static string GetClientPartition(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

public partial class Program
{
}
