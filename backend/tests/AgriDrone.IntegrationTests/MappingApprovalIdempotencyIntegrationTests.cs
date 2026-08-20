using AgriDrone.Database;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class MappingApprovalIdempotencyIntegrationTests
{
    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";
    private const string DatabaseName = "agridrone_step1_mapping";
    private const string ConnectionString =
        "Host=127.0.0.1;Port=55432;Database=" + DatabaseName +
        ";Username=agridrone_test;Password=agridrone_test";

    [Fact]
    public async Task SameApprovalWithDifferentMessageIdsDoesNotPublishAgain()
    {
        await RecreateDatabaseAsync();
        await ApplyMigrationsAsync();

        var approvalId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var existingMapId = Guid.NewGuid();
        await SeedExistingPublicationAsync(
            approvalId,
            missionId,
            farmId,
            zoneId,
            existingMapId);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] = ConnectionString,
                ["RabbitMq:Enabled"] = "false",
                ["Messaging:Outbox:Enabled"] = "false",
                ["Messaging:Retention:Enabled"] = "false",
                ["Redis:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationMessagingFoundation(configuration);
        services.AddMappingPublicationPersistence(configuration);
        services.AddSingleton<IEffectiveAccessService, AllowAllAccessService>();
        await using var provider = services.BuildServiceProvider();

        var payload = new MappingCandidatesApprovedV1(
            approvalId,
            missionId,
            farmId,
            zoneId,
            null,
            "step1-test",
            0,
            1,
            1,
            new Dictionary<string, string>(),
            []);
        var first = CreateEnvelope(payload, Guid.NewGuid());
        var second = CreateEnvelope(payload, Guid.NewGuid());

        await using (var firstScope = provider.CreateAsyncScope())
        {
            var handler = firstScope.ServiceProvider.GetRequiredService<
                IIntegrationMessageHandler<MappingCandidatesApprovedV1>>();
            var result = await handler.HandleAsync(first, CancellationToken.None);
            Assert.Equal(
                IntegrationMessageDisposition.Acknowledge,
                result.Disposition);
        }

        await using (var secondScope = provider.CreateAsyncScope())
        {
            var handler = secondScope.ServiceProvider.GetRequiredService<
                IIntegrationMessageHandler<MappingCandidatesApprovedV1>>();
            var result = await handler.HandleAsync(second, CancellationToken.None);
            Assert.Equal(
                IntegrationMessageDisposition.Acknowledge,
                result.Disposition);
        }

        await using var verification = new NpgsqlConnection(ConnectionString);
        await verification.OpenAsync();
        Assert.Equal(
            1L,
            await CountAsync(
                verification,
                "SELECT COUNT(*) FROM farm.zone_map_versions WHERE source_approval_id = @approval_id",
                approvalId));
        Assert.Equal(
            0L,
            await CountAsync(
                verification,
                "SELECT COUNT(*) FROM system.outbox_messages",
                approvalId));
        Assert.Equal(
            2L,
            await CountAsync(
                verification,
                "SELECT COUNT(*) FROM system.inbox_messages WHERE message_id IN (@first, @second)",
                approvalId,
                first.MessageId,
                second.MessageId));
    }

    private static IntegrationEventEnvelope<MappingCandidatesApprovedV1>
        CreateEnvelope(MappingCandidatesApprovedV1 payload, Guid messageId) =>
        IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.MappingCandidatesApprovedV1,
            messageId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            payload);

    private static async Task ApplyMigrationsAsync()
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
        PostgreSqlEnumMappings.ConfigureDataSource(dataSourceBuilder);
        await using var dataSource = dataSourceBuilder.Build();
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>()
            .UseNpgsql(
                dataSource,
                npgsql =>
                {
                    npgsql.UseNetTopologySuite();
                    npgsql.MigrationsAssembly(
                        typeof(AgriDroneSchemaDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable(
                        "__ef_migrations_history",
                        "system");
                })
            .Options;
        await using var context = new AgriDroneSchemaDbContext(options);
        await context.Database.MigrateAsync();
    }

    private static async Task SeedExistingPublicationAsync(
        Guid approvalId,
        Guid missionId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SET session_replication_role = replica;
            INSERT INTO farm.zone_map_versions
                (id, farm_id, zone_id, source_mission_id,
                 source_approval_id, version_number, status,
                 grid_bearing_deg, row_spacing_m, plant_spacing_m,
                 algorithm_version, parameters, created_at)
            VALUES
                (@map_id, @farm_id, @zone_id, @mission_id,
                 @approval_id, 1, 'DRAFT',
                 0, 1, 1, 'step1-test', '{}'::jsonb, NOW());
            SET session_replication_role = DEFAULT;
            """;
        command.Parameters.AddWithValue("map_id", mapVersionId);
        command.Parameters.AddWithValue("farm_id", farmId);
        command.Parameters.AddWithValue("zone_id", zoneId);
        command.Parameters.AddWithValue("mission_id", missionId);
        command.Parameters.AddWithValue("approval_id", approvalId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(
        NpgsqlConnection connection,
        string sql,
        Guid unused,
        Guid? first = null,
        Guid? second = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (sql.Contains("@approval_id", StringComparison.Ordinal))
        {
            command.Parameters.AddWithValue("approval_id", unused);
        }

        if (first.HasValue)
        {
            command.Parameters.AddWithValue("first", first.Value);
            command.Parameters.AddWithValue("second", second!.Value);
        }

        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task RecreateDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var drop = connection.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE)";
        await drop.ExecuteNonQueryAsync();
        await using var create = connection.CreateCommand();
        create.CommandText = $"CREATE DATABASE {DatabaseName}";
        await create.ExecuteNonQueryAsync();
    }

    private sealed class AllowAllAccessService : IEffectiveAccessService
    {
        public Task<AccessDecision> CheckTenantAsync(
            Guid actorId,
            Guid tenantId,
            TenantAccessLevel requiredAccess,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AccessDecision.Allow());

        public Task<AccessDecision> CheckFarmAsync(
            Guid actorId,
            Guid tenantId,
            Guid farmId,
            FarmAccessLevel requiredAccess,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AccessDecision.Allow());

        public Task<AccessDecision> CheckZoneAsync(
            Guid actorId,
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            FarmAccessLevel requiredAccess,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AccessDecision.Allow());
    }
}
