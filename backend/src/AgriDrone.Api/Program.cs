using AgriDrone.Api.Legacy;
using AgriDrone.Api.ModelBinding;
using AgriDrone.Api.Reporting;
using AgriDrone.Api.Surveys;
using AgriDrone.Api.Swagger;
using AgriDrone.Database;
using AgriDrone.Integrations.AI;
using AgriDrone.Integrations.Email;
using AgriDrone.Integrations.Media;
using AgriDrone.Integrations.Media.Telemetry;
using AgriDrone.Integrations.SePay;
using AgriDrone.Modules.Farms;
using AgriDrone.Modules.Identity;
using AgriDrone.Modules.Missions;
using AgriDrone.Modules.Notifications;
using AgriDrone.Modules.Plants;
using AgriDrone.Modules.Surveys;
using AgriDrone.SharedInfrastructure.Authentication;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.ExceptionHandling;
using AgriDrone.SharedInfrastructure.Execution;
using AgriDrone.SharedInfrastructure.Health;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Validation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RevenueReportSnapshots>();

builder.Services
    .AddControllers()
    .AddMvcOptions(options =>
        options.ModelBinderProviders.Insert(
            0,
            new StringEnumModelBinderProvider()))
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.SnakeCaseUpper,
                allowIntegerValues: false)));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(
            JsonNamingPolicy.SnakeCaseUpper,
            allowIntegerValues: false)));

builder.Services
    .AddOptions<LegacyFeaturesOptions>()
    .Bind(builder.Configuration.GetSection(
        LegacyFeaturesOptions.SectionName))
    .PostConfigure(options =>
    {
        if (!builder.Environment.IsDevelopment())
        {
            options.EnableDeprecatedEndpoints = false;
        }
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<LegacyEndpointOperationFilter>();
    options.OperationFilter<AllowAnonymousOperationFilter>();

    var xmlFileName =
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlFilePath = Path.Combine(
        AppContext.BaseDirectory,
        xmlFileName);

    options.IncludeXmlComments(xmlFilePath);

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token"
    });

    options.AddSecurityRequirement(document =>
    new OpenApiSecurityRequirement
    {
        [
            new OpenApiSecuritySchemeReference(
                "Bearer",
                document)
        ] = []
    });
});

// Forwarded Headers for reverse proxy (Caddy)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust internal container network ranges (Docker standard bridge subnets)
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("192.168.0.0/16"));
    options.ForwardLimit = 1;
});

// CORS
builder.Services.AddCors(options =>
{
    var corsOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()?
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin.Trim().TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? [];

    var refreshAuthOrigins = builder.Configuration
        .GetSection("RefreshSession:AllowedOrigins")
        .Get<string[]>()?
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin.Trim().TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? [];

    var effectiveAuthOrigins = refreshAuthOrigins.Length > 0 ? refreshAuthOrigins : corsOrigins;

    options.AddPolicy("Frontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy
                .SetIsOriginAllowed(_ => true)
                .AllowCredentials()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            if (corsOrigins.Length > 0)
            {
                policy
                    .WithOrigins(corsOrigins)
                    .AllowCredentials()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
            else
            {
                // Fail-closed: No origins permitted when not configured
                policy.WithOrigins();
            }
        }
    });

    options.AddPolicy("Auth", policy =>
    {
        if (effectiveAuthOrigins.Length > 0)
        {
            policy
                .WithOrigins(effectiveAuthOrigins)
                .AllowCredentials()
                .WithHeaders("Content-Type", "Authorization", "X-AgriDrone-Session")
                .WithMethods("POST", "OPTIONS");
        }
        else
        {
            // Fail-closed: Auth cookie routes never use wildcard origins
            policy.WithOrigins();
        }
    });
});

var publicSubmitPermitLimit = Math.Max(1, builder.Configuration.GetValue(
    "RateLimiting:V3PublicSurvey:SubmitPermitLimit", 5));
var publicTrackPermitLimit = Math.Max(1, builder.Configuration.GetValue(
    "RateLimiting:V3PublicSurvey:TrackPermitLimit", 30));
var publicWindowSeconds = Math.Max(1, builder.Configuration.GetValue(
    "RateLimiting:V3PublicSurvey:WindowSeconds", 60));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(V3PublicRateLimitPolicies.Submit, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetPublicRateLimitPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = publicSubmitPermitLimit,
                Window = TimeSpan.FromSeconds(publicWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy(V3PublicRateLimitPolicies.Track, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetPublicRateLimitPartition(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = publicTrackPermitLimit,
                Window = TimeSpan.FromSeconds(publicWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Modules
builder.Services
    .AddEmailIntegration(builder.Configuration)
    .AddMediaIntegration(builder.Configuration)
    .AddTelemetryNormalization(builder.Configuration)
    .AddAgriDroneDatabase(builder.Configuration)
    .AddFarmsModule(builder.Configuration)
    .AddIdentityModule(builder.Configuration)
    .AddMissionsModule(builder.Configuration)
    .AddNotificationsModule(builder.Configuration)
    .AddPlantsModule(builder.Configuration)
    .AddSurveysModule(builder.Configuration)
    .AddSePayIntegration(builder.Configuration)
    .AddAiIntegration(builder.Configuration)
    .AddIntegrationMessagingFoundation(builder.Configuration)
    .AddMappingPublicationPersistence(builder.Configuration)
    .AddSurveyResultPublicationPersistence(builder.Configuration)
    .AddSurveyApprovalPersistence(builder.Configuration)
    .AddSurveyBoundaryVerificationPersistence(builder.Configuration)
    .AddSurveyAppointmentSchedulingPersistence(builder.Configuration)
    .AddSurveyPricingConfirmationPersistence(builder.Configuration)
    .AddSurveyPaymentProcessingPersistence(builder.Configuration)
    .AddAgriDroneHealthChecks()
    .AddExecutionContext()
    .AddJwtAuthentication(builder.Configuration)
    .AddAccessAuthorization()
    .AddValidationPipeline()
    .AddGlobalExceptionHandling();

builder.Services.AddSingleton<IPublicSurveyRequestCallerScope,
    PublicSurveyRequestCallerScope>();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>(
        "DatabaseInitialization:ApplyMigrationsOnStartup"))
{
    await app.Services.MigrateAgriDroneDatabaseAsync();
}

if (builder.Configuration.GetValue(
        "DatabaseInitialization:ValidateCoreMasterDataOnStartup",
        true))
{
    await app.Services.ValidateCoreMasterDataAsync();
}
await app.Services.BootstrapSystemAdminAsync();

// Purge orphaned survey authorization evidences from MinIO on startup
await app.Services.PurgeOrphanedSurveyAuthorizationsAsync();


app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.InjectJavascript("/swagger/folder-upload.js");
        options.InjectJavascript("/swagger/sepay-qr.js");
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseRateLimiter();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseExecutionContext();
app.UseMiddleware<LegacyEndpointGateMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live")
    });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready")
    });
app.MapHealthChecks(
    "/health/minio",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("minio")
    });
app.Run();

static string GetPublicRateLimitPartition(HttpContext httpContext) =>
    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
