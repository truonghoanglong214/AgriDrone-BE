using AgriDrone.Integrations.Media;
using AgriDrone.Integrations.Media.Telemetry;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Media;

public sealed class MediaDependencyInjectionTests
{
    [Fact]
    public void DisabledObjectStorageStillCompletesTheDependencyGraph()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddMediaIntegration(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        var storage = provider.GetRequiredService<IObjectStorage>();
        var writer = provider.GetRequiredService<IObjectStorageWriter>();

        Assert.Same(storage, writer);
    }

    [Fact]
    public async Task DisabledTelemetryDecoderStillCompletesTheDependencyGraph()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telemetry:BlackboxDecoder:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddTelemetryNormalization(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        var normalizer = provider.GetRequiredService<ITelemetryLogNormalizer>();
        await using var content = new MemoryStream([1]);

        var result = await normalizer.NormalizeAsync(content, "flight.bbl");

        Assert.True(result.IsFailure);
        Assert.Equal("Telemetry.DecoderDisabled", result.Error.Code);
    }
}
