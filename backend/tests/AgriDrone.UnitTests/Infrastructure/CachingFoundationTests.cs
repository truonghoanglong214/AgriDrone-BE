using AgriDrone.IntegrationContracts.Plants;
using AgriDrone.SharedInfrastructure.Caching;
using AgriDrone.SharedInfrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure;

public sealed class CachingFoundationTests
{
    [Fact]
    public async Task DisabledRedisFallsBackToDatabaseSource()
    {
        var source = new StubPlantReferenceSource();
        await using var provider = CreateServiceProvider(
            redisEnabled: false,
            source);
        var query = provider.GetRequiredService<IPlantReferenceQuery>();

        var result = await query.GetActiveByZoneAsync(
            StubPlantReferenceSource.TenantId,
            StubPlantReferenceSource.FarmId,
            StubPlantReferenceSource.ZoneId,
            StubPlantReferenceSource.MapVersionId);

        Assert.Equal(1, source.LoadCount);
        Assert.Equal(StubPlantReferenceSource.PlantId, Assert.Single(result).PlantId);
    }

    [Fact]
    public async Task UnavailableRedisFallsBackToDatabaseSource()
    {
        var source = new StubPlantReferenceSource();
        await using var provider = CreateServiceProvider(
            redisEnabled: true,
            source);
        var query = provider.GetRequiredService<IPlantReferenceQuery>();

        var result = await query.GetActiveByZoneAsync(
            StubPlantReferenceSource.TenantId,
            StubPlantReferenceSource.FarmId,
            StubPlantReferenceSource.ZoneId,
            StubPlantReferenceSource.MapVersionId);

        Assert.Equal(1, source.LoadCount);
        Assert.Equal(StubPlantReferenceSource.PlantId, Assert.Single(result).PlantId);
    }

    private static ServiceProvider CreateServiceProvider(
        bool redisEnabled,
        IPlantReferenceSource source)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:AgriDrone"] =
                "Host=localhost;Database=unused;Username=test;Password=test",
            ["RabbitMq:Enabled"] = "false",
            ["Messaging:Outbox:Enabled"] = "false",
            ["Messaging:Retention:Enabled"] = "false",
            ["Redis:Enabled"] = redisEnabled.ToString(),
            ["Redis:ConnectionString"] =
                "127.0.0.1:1,abortConnect=false,connectTimeout=100,asyncTimeout=100",
            ["Redis:InstancePrefix"] = "agridrone-unit-tests",
            ["Redis:PlantReferenceTtlSeconds"] = "30",
            ["Redis:InvalidationEpochTtlSeconds"] = "30"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationMessagingFoundation(configuration);
        services.AddSingleton(source);
        return services.BuildServiceProvider();
    }

    private sealed class StubPlantReferenceSource : IPlantReferenceSource
    {
        public static readonly Guid TenantId = Guid.NewGuid();
        public static readonly Guid FarmId = Guid.NewGuid();
        public static readonly Guid ZoneId = Guid.NewGuid();
        public static readonly Guid MapVersionId = Guid.NewGuid();
        public static readonly Guid PlantId = Guid.NewGuid();

        public int LoadCount { get; private set; }

        public Task<IReadOnlyList<PlantReferenceV1>> LoadActiveByZoneAsync(
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            Guid mapVersionId,
            CancellationToken cancellationToken = default)
        {
            LoadCount++;
            IReadOnlyList<PlantReferenceV1> result =
            [
                new PlantReferenceV1(
                    PlantId,
                    FarmId,
                    ZoneId,
                    "ACTIVE",
                    MapVersionId,
                    10.5,
                    106.5,
                    1,
                    1,
                    0.25)
            ];
            return Task.FromResult(result);
        }
    }
}
