using AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.Integrations.Media.Telemetry;

public static class TelemetryIntegrationExtensions
{
    public static IServiceCollection AddTelemetryNormalization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<BlackboxDecoderOptions>()
            .Bind(configuration.GetSection(
                BlackboxDecoderOptions.SectionName))
            .Validate(
                options =>
                    !options.Enabled ||
                    Path.IsPathFullyQualified(options.ExecutablePath) &&
                    File.Exists(options.ExecutablePath),
                "Configure an existing absolute Blackbox decoder path.")
            .Validate(
                options => options.TimeoutSeconds is >= 1 and <= 120,
                "Decoder timeout must be between 1 and 120 seconds.")
            .Validate(
                options =>
                    options.MaximumFileBytes > 0 &&
                    options.MaximumFileBytes <= 20L * 1024 * 1024,
                "Maximum log size must be between 1 byte and 20 MiB.")
            .ValidateOnStart();

        var configuredOptions = configuration
            .GetSection(BlackboxDecoderOptions.SectionName)
            .Get<BlackboxDecoderOptions>()
            ?? new BlackboxDecoderOptions();

        if (!configuredOptions.Enabled)
        {
            services.AddSingleton<
                ITelemetryLogNormalizer,
                DisabledTelemetryLogNormalizer>();
            return services;
        }

        // One decoder process at a time per application instance.
        services.AddSingleton<
            ITelemetryLogNormalizer,
            BlackboxTelemetryLogNormalizer>();

        return services;
    }
}
