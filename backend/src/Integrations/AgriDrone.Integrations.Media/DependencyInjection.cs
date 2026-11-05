using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Minio;

namespace AgriDrone.Integrations.Media;

public static class DependencyInjection
{
    public static IServiceCollection AddMediaIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<MinioStorageOptions>()
            .Bind(
                configuration.GetSection(
                    MinioStorageOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<
            IValidateOptions<MinioStorageOptions>,
            MinioStorageOptionsValidator>();

        services.TryAddSingleton<TimeProvider>(
            TimeProvider.System);

        var configuredOptions = configuration
            .GetSection(MinioStorageOptions.SectionName)
            .Get<MinioStorageOptions>()
            ?? new MinioStorageOptions();

        services.TryAddSingleton<
            IChecksumCalculator, Sha256ChecksumCalculator>();

        if (!configuredOptions.Enabled)
        {
            services.AddSingleton<DisabledObjectStorage>();
            services.AddSingleton<IObjectStorage>(serviceProvider =>
                serviceProvider.GetRequiredService<DisabledObjectStorage>());
            services.AddSingleton<IObjectStorageWriter>(serviceProvider =>
                serviceProvider.GetRequiredService<DisabledObjectStorage>());
            services.AddSingleton<IMultipartObjectStorage>(serviceProvider =>
                serviceProvider.GetRequiredService<DisabledObjectStorage>());
            services.AddSingleton<ISurveyAuthorizationStorageCleaner>(sp =>
                new SurveyAuthorizationStorageCleaner(
                    sp.GetService<IMinioClient>(),
                    sp.GetRequiredService<IOptions<MinioStorageOptions>>(),
                    sp.GetRequiredService<ILogger<SurveyAuthorizationStorageCleaner>>()));
            return services;
        }

        services.AddSingleton<IMinioClient>(
            serviceProvider =>
            {
                var options = serviceProvider
                    .GetRequiredService<
                        IOptions<MinioStorageOptions>>()
                    .Value;

                var client = new MinioClient()
                    .WithEndpoint(
                        new Uri(options.Endpoint))
                    .WithCredentials(
                        options.AccessKey,
                        options.SecretKey)
                    .WithSSL(options.UseSsl)
                    .Build();

                return client;
            });

        services.AddSingleton<
            IObjectStorage,
            MinioObjectStorage>();
        services.AddSingleton<IObjectStorageWriter>(provider =>
            (MinioObjectStorage)provider.GetRequiredService<IObjectStorage>());
        services.AddSingleton<IMultipartObjectStorage, MinioMultipartObjectStorage>();
        services.AddSingleton<ISurveyAuthorizationStorageCleaner, SurveyAuthorizationStorageCleaner>();

        services
            .AddHealthChecks()
            .AddCheck<MinioReadinessHealthCheck>(
                "minio",
                tags: ["ready", "minio"]);

        return services;
    }

    public static async Task PurgeOrphanedSurveyAuthorizationsAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var cleaner = scope.ServiceProvider.GetService<ISurveyAuthorizationStorageCleaner>();
        if (cleaner is null)
        {
            return;
        }

        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?
            .CreateLogger("AgriDrone.Integrations.Media.StorageCleaner");
        try
        {
            var purgedCount = await cleaner.PurgeOrphanedEvidencesAsync(cancellationToken);
            if (purgedCount > 0)
            {
                logger?.LogInformation("Successfully purged {Count} orphaned survey authorization evidence files from MinIO", purgedCount);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to purge orphaned survey authorization evidence files from MinIO");
        }
    }
}
