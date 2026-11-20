using AgriDrone.Database;
using AgriDrone.Database.Surveys;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class V303B2MigrationIntegrationTests
{
    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";
    private const string PreviousMigration =
        "20261114124442_V3_03B1CaptureAttemptPersistenceExpand";
    private const string CurrentMigration =
        "20261115020928_V3_03B2FundingCancellationPersistence";

    [Fact]
    public async Task FreshAndPopulatedLegacyUpgradePreserveDataAndGuardDurableFacts()
    {
        var freshDatabase = NewDatabaseName("fresh");
        var upgradeDatabase = NewDatabaseName("legacy");
        try
        {
            await RecreateDatabaseAsync(freshDatabase);
            await using (var context = CreateContext(freshDatabase))
            {
                await context.Database.MigrateAsync(CurrentMigration);
                await AssertExpandedSchemaAsync(freshDatabase);
                await context.Database.MigrateAsync(PreviousMigration);
                Assert.Equal(PreviousMigration, await LatestMigrationAsync(freshDatabase));
                await context.Database.MigrateAsync(CurrentMigration);
                await AssertExpandedSchemaAsync(freshDatabase);
            }

            await RecreateDatabaseAsync(upgradeDatabase);
            var legacy = await SeedLegacyOrderAsync(upgradeDatabase);
            await using (var context = CreateContext(upgradeDatabase))
            {
                await context.Database.MigrateAsync(CurrentMigration);
                await AssertExpandedSchemaAsync(upgradeDatabase);
                await AssertLegacyOrderPreservedAsync(upgradeDatabase, legacy.OrderId);
                await context.Database.MigrateAsync(PreviousMigration);
                await context.Database.MigrateAsync(CurrentMigration);
            }

            await InsertDurableReceiptAsync(upgradeDatabase, legacy.TenantId, legacy.OrderId);
            await using (var context = CreateContext(upgradeDatabase))
            {
                var exception = await Record.ExceptionAsync(() =>
                    context.Database.MigrateAsync(PreviousMigration));
                var postgres = FindPostgresException(exception);
                Assert.NotNull(postgres);
                Assert.Equal("P0001", postgres.SqlState);
                Assert.Equal(CurrentMigration, await LatestMigrationAsync(upgradeDatabase));
            }
        }
        finally
        {
            await DropDatabaseAsync(freshDatabase);
            await DropDatabaseAsync(upgradeDatabase);
        }
    }

    [Fact]
    public async Task CaptureCoordinatorRejectsUnassignedManagerBeforeCommercialMutation()
    {
        var databaseName = NewDatabaseName("capture_auth");
        try
        {
            await RecreateDatabaseAsync(databaseName);
            var seeded = await SeedLegacyOrderAsync(databaseName);
            await using (var migration = CreateContext(databaseName))
            {
                await migration.Database.MigrateAsync(CurrentMigration);
            }

            await using var context = CreateContext(databaseName);
            var clock = TimeProvider.System;
            var coordinator = new SurveyCaptureExecutionCoordinator(
                context,
                clock,
                new OutboxMessageFactory(
                    new SystemTextJsonIntegrationMessageSerializer(),
                    clock));
            var result = await coordinator.PrepareAttemptAsync(
                new PrepareCaptureAttemptRequest(
                    seeded.OrderId,
                    0,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "authorization regression",
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "capture-auth-fingerprint"));

            Assert.False(result.Success);
            Assert.Equal("MANAGER_FORBIDDEN", result.ErrorCode);
            Assert.Equal(0, await context.Set<AgriDrone.Modules.Surveys.Domain.CaptureAttempt>().CountAsync());
            Assert.Equal(0, await context.Set<AgriDrone.SharedInfrastructure.Messaging.Persistence.OutboxMessage>().CountAsync());
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    private static async Task<(Guid TenantId, Guid OrderId)> SeedLegacyOrderAsync(
        string databaseName)
    {
        await using var context = CreateContext(databaseName);
        await context.Database.MigrateAsync(PreviousMigration);
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        const string serviceId = "10000000-0000-0000-0000-000000000001";

        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"""
            INSERT INTO identity.users (id, email, password_hash, full_name)
            VALUES ('{actorId}', 'v303-{actorId:N}@example.test', 'not-used', 'V3-03 Legacy User');
            INSERT INTO identity.tenants (id, code, name)
            VALUES ('{tenantId}', 'V303-{tenantId:N}', 'V3-03 Legacy Tenant');
            INSERT INTO farm.farms (id, tenant_id, code, name, created_by)
            VALUES ('{farmId}', '{tenantId}', 'V303-F', 'V3-03 Legacy Farm', '{actorId}');
            INSERT INTO survey.survey_requests
                (id, request_number, kind, tenant_id, farm_id, requested_by_user_id,
                 survey_service_id, caller_scope, idempotency_key, applicant_name,
                 applicant_email, applicant_phone, farm_name, farm_address,
                 approximate_area_ha, map_location, status)
            VALUES
                ('{requestId}', 'REQ-{requestId:N}', 'EXISTING_FARM_SURVEY', '{tenantId}', '{farmId}', '{actorId}',
                 '{serviceId}', 'tenant:{tenantId}', 'idem-{requestId:N}', 'Applicant',
                 'v303@example.test', '0900000000', 'V3-03 Legacy Farm', 'Test address',
                 1.0000, ST_SetSRID(ST_MakePoint(5, 5), 4326), 'SUBMITTED');
            INSERT INTO survey.survey_orders
                (id, order_number, tenant_id, farm_id, survey_request_id,
                 survey_service_id, requires_baseline_mapping, status,
                 deposit_amount, deposit_payment_status)
            VALUES
                ('{orderId}', 'ORD-{orderId:N}', '{tenantId}', '{farmId}', '{requestId}',
                 '{serviceId}', FALSE, 'PENDING_BOUNDARY_VERIFICATION',
                 5000, 'PENDING'::system.survey_payment_status);
            """);
        return (tenantId, orderId);
    }

    private static async Task AssertExpandedSchemaAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();
        Assert.Equal(CurrentMigration, await LatestMigrationAsync(connection));
        Assert.Equal(6L, await ScalarAsync<long>(connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'survey' AND table_name IN ('survey_deposit_obligations','survey_funding_receipts','survey_funding_allocations','survey_cancellation_decisions','survey_refund_obligations','cancellation_retentions')"));
        Assert.Equal(1L, await ScalarAsync<long>(connection,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'survey' AND table_name = 'survey_cancellation_decisions' AND column_name = 'operation_id'"));
        Assert.Equal(1L, await ScalarAsync<long>(connection,
            "SELECT count(*) FROM pg_constraint WHERE conname = 'fk_survey_orders_deposit_obligation_order' AND condeferrable AND condeferred"));
        Assert.Equal(1L, await ScalarAsync<long>(connection,
            "SELECT count(*) FROM pg_enum JOIN pg_type ON pg_type.oid = pg_enum.enumtypid JOIN pg_namespace ON pg_namespace.oid = pg_type.typnamespace WHERE pg_namespace.nspname = 'system' AND pg_type.typname = 'mission_purpose' AND enumlabel = 'CAPTURE'"));
    }

    private static async Task AssertLegacyOrderPreservedAsync(
        string databaseName,
        Guid orderId)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();
        Assert.Equal("LEGACY", await ScalarAsync<string>(connection,
            $"SELECT scope::text FROM survey.survey_orders WHERE id = '{orderId}'"));
        Assert.Equal(0L, await ScalarAsync<long>(connection,
            $"SELECT count(*) FROM survey.survey_orders WHERE id = '{orderId}' AND deposit_obligation_id IS NOT NULL"));
        Assert.Equal(0L, await ScalarAsync<long>(connection,
            $"SELECT count(*) FROM survey.survey_deposit_obligations WHERE survey_order_id = '{orderId}'"));
    }

    private static async Task InsertDurableReceiptAsync(
        string databaseName,
        Guid tenantId,
        Guid orderId)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();
        await ExecuteAsync(connection, $"""
            INSERT INTO survey.survey_funding_receipts
                (id, tenant_id, survey_order_id, deposit_obligation_id, provider,
                 provider_transaction_id, amount, currency, payment_reference,
                 payload_fingerprint, received_at, created_at)
            VALUES
                ('{Guid.NewGuid()}', '{tenantId}', '{orderId}', NULL, 'TEST',
                 'TX-{Guid.NewGuid():N}', 5000, 'VND', 'LEGACY-RECEIPT',
                 'FINGERPRINT-{Guid.NewGuid():N}', NOW(), NOW());
            """);
    }

    private static AgriDroneSchemaDbContext CreateContext(string databaseName)
    {
        var builder = new NpgsqlDataSourceBuilder(BuildConnectionString(databaseName));
        PostgreSqlEnumMappings.ConfigureDataSource(builder);
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
        AgriDroneSchemaDbContextOptions.Configure(options, builder.Build());
        return new AgriDroneSchemaDbContext(options.Options);
    }

    private static PostgresException? FindPostgresException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is PostgresException postgres) return postgres;
            exception = exception.InnerException;
        }
        return null;
    }

    private static Task<string> LatestMigrationAsync(string databaseName) =>
        WithConnectionAsync(databaseName, connection => LatestMigrationAsync(connection));

    private static Task<string> LatestMigrationAsync(NpgsqlConnection connection) =>
        ScalarAsync<string>(connection,
            "SELECT \"MigrationId\" FROM system.__ef_migrations_history ORDER BY \"MigrationId\" DESC LIMIT 1");

    private static async Task<T> WithConnectionAsync<T>(
        string databaseName,
        Func<NpgsqlConnection, Task<T>> action)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();
        return await action(connection);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Assert.IsType<T>(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RecreateDatabaseAsync(string databaseName)
    {
        await DropDatabaseAsync(databaseName);
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildConnectionString(string databaseName) =>
        $"Host=127.0.0.1;Port=55432;Database={databaseName};Username=agridrone_test;Password=agridrone_test";

    private static string NewDatabaseName(string suffix) =>
        $"agridrone_v303b2_{suffix}_{Guid.NewGuid():N}";
}
