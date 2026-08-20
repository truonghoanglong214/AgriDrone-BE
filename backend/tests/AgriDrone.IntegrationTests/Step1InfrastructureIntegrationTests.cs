using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Plants;
using AgriDrone.SharedInfrastructure.Caching;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class Step1InfrastructureIntegrationTests
{
    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";
    private const string InboxDatabase = "agridrone_step1_inbox";
    private const string InboxConnection =
        "Host=127.0.0.1;Port=55432;Database=" + InboxDatabase +
        ";Username=agridrone_test;Password=agridrone_test";

    [Fact]
    public async Task InboxReplayAndRollbackAreAtomicOnPostgreSql()
    {
        await RecreateDatabaseAsync(InboxDatabase);
        var options = new DbContextOptionsBuilder<InboxTestDbContext>()
            .UseNpgsql(InboxConnection)
            .Options;

        await using (var setup = new InboxTestDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        var envelope = new IntegrationEventEnvelope<string>(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            1,
            "tests.inbox.v1",
            "payload");
        var coordinator = new InboxExecutionCoordinator(TimeProvider.System);

        await using (var retryContext = new InboxTestDbContext(options))
        {
            var retry = await coordinator.ExecuteAsync(
                retryContext,
                "step1-inbox-consumer",
                envelope,
                async (context, token) =>
                {
                    context.BusinessRecords.Add(
                        new BusinessRecord { Id = Guid.NewGuid() });
                    await context.SaveChangesAsync(token);
                    return InboxHandlerResult.Retry("transient failure");
                });

            Assert.Equal(
                IntegrationMessageDisposition.Retry,
                retry.Disposition);
        }

        await using (var rolledBack = new InboxTestDbContext(options))
        {
            Assert.Empty(await rolledBack.BusinessRecords.ToListAsync());
            Assert.Empty(await rolledBack.InboxMessages.ToListAsync());
        }

        var handlerCalls = 0;
        await using (var completedContext = new InboxTestDbContext(options))
        {
            var completed = await coordinator.ExecuteAsync(
                completedContext,
                "step1-inbox-consumer",
                envelope,
                async (context, token) =>
                {
                    handlerCalls++;
                    context.BusinessRecords.Add(
                        new BusinessRecord { Id = Guid.NewGuid() });
                    await context.SaveChangesAsync(token);
                    return InboxHandlerResult.Completed("{\"ok\":true}");
                });
            Assert.Equal(
                IntegrationMessageDisposition.Acknowledge,
                completed.Disposition);
        }

        await using (var replayContext = new InboxTestDbContext(options))
        {
            var replay = await coordinator.ExecuteAsync(
                replayContext,
                "step1-inbox-consumer",
                envelope,
                (_, _) =>
                {
                    handlerCalls++;
                    return Task.FromResult(
                        InboxHandlerResult.Completed("unexpected"));
                });
            Assert.Equal(
                IntegrationMessageDisposition.Acknowledge,
                replay.Disposition);
        }

        await using var verification = new InboxTestDbContext(options);
        Assert.Equal(1, handlerCalls);
        Assert.Single(await verification.BusinessRecords.ToListAsync());
        var inbox = Assert.Single(
            await verification.InboxMessages.ToListAsync());
        Assert.Equal(InboxMessageStatus.Completed, inbox.Status);
    }

    [Fact]
    public async Task RedisKeysAreTenantScopedExpiringAndInvalidatedPerZone()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Enabled"] = "true",
                ["Redis:ConnectionString"] =
                    "127.0.0.1:56379,password=agridrone_test,abortConnect=false",
                ["Redis:InstancePrefix"] = "agridrone-step1-tests",
                ["Redis:PlantReferenceTtlSeconds"] = "60",
                ["Redis:InvalidationEpochTtlSeconds"] = "300"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisCachingFoundation(configuration);
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IPlantReferenceCache>();
        var concreteCache = Assert.IsType<RedisPlantReferenceCache>(cache);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var mapVersionId = Guid.NewGuid();
        var referencesA = CreateReferences(farmId, zoneId, mapVersionId);
        var referencesB = CreateReferences(farmId, zoneId, mapVersionId);

        await cache.SetAsync(
            tenantA, farmId, zoneId, mapVersionId, referencesA);
        await cache.SetAsync(
            tenantB, farmId, zoneId, mapVersionId, referencesB);

        var keyA = concreteCache.GetDataKeyForDiagnostics(
            tenantA, farmId, zoneId, mapVersionId);
        var keyB = concreteCache.GetDataKeyForDiagnostics(
            tenantB, farmId, zoneId, mapVersionId);
        Assert.NotEqual(keyA, keyB);
        var cachedA = await cache.TryGetAsync(
            tenantA, farmId, zoneId, mapVersionId);
        var cachedB = await cache.TryGetAsync(
            tenantB, farmId, zoneId, mapVersionId);
        Assert.NotNull(cachedA);
        Assert.NotNull(cachedB);
        Assert.Equal(referencesA[0].PlantId, Assert.Single(cachedA).PlantId);
        Assert.Equal(referencesB[0].PlantId, Assert.Single(cachedB).PlantId);

        await using var redis = await ConnectionMultiplexer.ConnectAsync(
            "127.0.0.1:56379,password=agridrone_test");
        var ttl = await redis.GetDatabase().KeyTimeToLiveAsync(keyA);
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value.TotalSeconds, 1, 60);

        await cache.InvalidateZoneAsync(tenantA, farmId, zoneId);
        Assert.Null(await cache.TryGetAsync(
            tenantA, farmId, zoneId, mapVersionId));
        Assert.NotNull(await cache.TryGetAsync(
            tenantB, farmId, zoneId, mapVersionId));
    }

    [Fact]
    public async Task RedisOutageFallsBackToPostgreSqlReadSource()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Enabled"] = "true",
                ["Redis:ConnectionString"] =
                    "127.0.0.1:1,abortConnect=false,connectTimeout=100,asyncTimeout=100",
                ["Redis:InstancePrefix"] = "agridrone-step1-fallback",
                ["Redis:PlantReferenceTtlSeconds"] = "60",
                ["Redis:InvalidationEpochTtlSeconds"] = "300"
            })
            .Build();
        var source = new PostgreSqlPlantReferenceSource();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisCachingFoundation(configuration);
        services.AddSingleton<IPlantReferenceSource>(source);
        await using var provider = services.BuildServiceProvider();
        var query = provider.GetRequiredService<IPlantReferenceQuery>();

        var result = await query.GetActiveByZoneAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.Equal(1, source.LoadCount);
        Assert.Equal(source.PlantId, Assert.Single(result).PlantId);
    }

    private static IReadOnlyList<PlantReferenceV1> CreateReferences(
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId) =>
    [
        new PlantReferenceV1(
            Guid.NewGuid(),
            farmId,
            zoneId,
            "ACTIVE",
            mapVersionId,
            10.5,
            106.5,
            1,
            1,
            0.25)
    ];

    private static async Task RecreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE); " +
            $"CREATE DATABASE {databaseName};";
        await command.ExecuteNonQueryAsync();
    }

    private sealed class InboxTestDbContext(
        DbContextOptions<InboxTestDbContext> options) : DbContext(options)
    {
        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        public DbSet<BusinessRecord> BusinessRecords => Set<BusinessRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
            modelBuilder.Entity<BusinessRecord>(builder =>
            {
                builder.ToTable("business_records", "tests");
                builder.HasKey(record => record.Id);
                builder.Property(record => record.Id).ValueGeneratedNever();
            });
        }
    }

    private sealed class BusinessRecord
    {
        public Guid Id { get; init; }
    }

    private sealed class PostgreSqlPlantReferenceSource
        : IPlantReferenceSource
    {
        public Guid PlantId { get; } = Guid.NewGuid();

        public int LoadCount { get; private set; }

        public async Task<IReadOnlyList<PlantReferenceV1>>
            LoadActiveByZoneAsync(
                Guid tenantId,
                Guid farmId,
                Guid zoneId,
                Guid mapVersionId,
                CancellationToken cancellationToken = default)
        {
            await using var connection = new NpgsqlConnection(AdminConnection);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            Assert.Equal(1, await command.ExecuteScalarAsync(cancellationToken));
            LoadCount++;
            return
            [
                new PlantReferenceV1(
                    PlantId,
                    farmId,
                    zoneId,
                    "ACTIVE",
                    mapVersionId,
                    null,
                    null,
                    null,
                    null,
                    null)
            ];
        }
    }
}
