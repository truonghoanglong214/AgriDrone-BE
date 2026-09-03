using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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

        if (!configuredOptions.Enabled)
        {
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

        services
            .AddHealthChecks()
            .AddCheck<MinioReadinessHealthCheck>(
                "minio",
                tags: ["ready", "minio"]);

        return services;
    }
}