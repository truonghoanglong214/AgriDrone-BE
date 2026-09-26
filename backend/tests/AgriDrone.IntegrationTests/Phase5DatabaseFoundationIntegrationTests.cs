using AgriDrone.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class Phase5DatabaseFoundationIntegrationTests
{
    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";

    private const string Phase4Migration =
        "20260925053119_Phase4ArchiveHarvestRuntimeModel";

    private const string Phase5Migration =
        "20260925100711_Phase5SurveyDatabaseFoundation";

    [Fact]
    public async Task FreshAndPhase4UpgradeCreateGuardedPhase5Schema()
    {
        var freshDatabase = NewDatabaseName("fresh");
        var upgradeDatabase = NewDatabaseName("upgrade");

        try
        {
            await RecreateDatabaseAsync(freshDatabase);
            await using (var freshContext = CreateContext(freshDatabase))
            {
                await freshContext.Database.MigrateAsync();
                await AssertPhase5SchemaAsync(freshDatabase);
            }

            await RecreateDatabaseAsync(upgradeDatabase);
            await using (var upgradeContext = CreateContext(upgradeDatabase))
            {
                await upgradeContext.Database.MigrateAsync(Phase4Migration);
                await upgradeContext.Database.MigrateAsync();
                await AssertPhase5SchemaAsync(upgradeDatabase);

                await upgradeContext.Database.MigrateAsync(Phase4Migration);
                Assert.Equal(
                    0,
                    await CountSurveyTablesAsync(upgradeDatabase));

                await upgradeContext.Database.MigrateAsync();
                await AssertPhase5SchemaAsync(upgradeDatabase);
            }
        }
        finally
        {
            await DropDatabaseAsync(freshDatabase);
            await DropDatabaseAsync(upgradeDatabase);
        }
    }

    private static AgriDroneSchemaDbContext CreateContext(string databaseName)
    {
        var connectionString = BuildConnectionString(databaseName);
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        PostgreSqlEnumMappings.ConfigureDataSource(dataSourceBuilder);
        var dataSource = dataSourceBuilder.Build();
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
        AgriDroneSchemaDbContextOptions.Configure(options, dataSource);
        return new AgriDroneSchemaDbContext(options.Options);
    }

    private static async Task AssertPhase5SchemaAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();

        Assert.Equal(
            Phase5Migration,
            await ScalarAsync<string>(
                connection,
                "SELECT \"MigrationId\" FROM system.__ef_migrations_history ORDER BY \"MigrationId\" DESC LIMIT 1"));

        Assert.Equal(
            11L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'survey' AND table_type = 'BASE TABLE'"));

        Assert.Equal(
            2L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM survey.survey_services WHERE code IN ('PLANT_HEALTH', 'HARVEST_READINESS')"));

        Assert.Equal(
            "EXPERIMENTAL",
            await ScalarAsync<string>(
                connection,
                "SELECT status::text FROM survey.survey_services WHERE code = 'HARVEST_READINESS'"));

        Assert.Equal(
            "YES",
            await ScalarAsync<string>(
                connection,
                "SELECT is_nullable FROM information_schema.columns WHERE table_schema = 'mission' AND table_name = 'drone_missions' AND column_name = 'survey_order_id'"));

        Assert.Equal(
            18,
            await ScalarAsync<int>(
                connection,
                "SELECT numeric_precision FROM information_schema.columns WHERE table_schema = 'survey' AND table_name = 'survey_orders' AND column_name = 'final_price'"));

        Assert.Equal(
            2,
            await ScalarAsync<int>(
                connection,
                "SELECT numeric_scale FROM information_schema.columns WHERE table_schema = 'survey' AND table_name = 'survey_orders' AND column_name = 'final_price'"));

        Assert.Equal(
            1L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM pg_constraint WHERE conname = 'ex_survey_service_prices_no_overlap' AND contype = 'x'"));

        Assert.Equal(
            1L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM pg_constraint WHERE conname = 'fk_drone_missions_orders_same_tenant_farm'"));

        Assert.Equal(
            1L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM pg_trigger WHERE tgname = 'trg_survey_service_prices_immutable_when_referenced' AND NOT tgisinternal"));

        Assert.Equal(
            1L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM pg_trigger WHERE tgname = 'trg_survey_orders_previous_compatible' AND NOT tgisinternal"));

        await AssertConstraintBehaviorAsync(connection);
    }

    private static async Task<long> CountSurveyTablesAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();
        return await ScalarAsync<long>(
            connection,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'survey' AND table_type = 'BASE TABLE'");
    }

    private static async Task AssertConstraintBehaviorAsync(NpgsqlConnection connection)
    {
        var actorId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var farmA = Guid.NewGuid();
        var farmB = Guid.NewGuid();
        var priceId = Guid.NewGuid();
        var requestA = Guid.NewGuid();
        var requestB = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        const string serviceId = "10000000-0000-0000-0000-000000000001";

        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO identity.users (id, email, password_hash, full_name)
            VALUES ('{actorId}', 'phase5-{actorId:N}@example.test', 'not-used', 'Phase 5 Test Actor');

            INSERT INTO identity.tenants (id, code, name)
            VALUES
                ('{tenantA}', 'P5-{tenantA:N}', 'Phase 5 Tenant A'),
                ('{tenantB}', 'P5-{tenantB:N}', 'Phase 5 Tenant B');

            INSERT INTO farm.farms (id, tenant_id, code, name, created_by)
            VALUES
                ('{farmA}', '{tenantA}', 'P5-A', 'Phase 5 Farm A', '{actorId}'),
                ('{farmB}', '{tenantB}', 'P5-B', 'Phase 5 Farm B', '{actorId}');

            INSERT INTO survey.survey_service_prices
                (id, survey_service_id, price_per_ha, currency, effective_from, created_by)
            VALUES
                ('{priceId}', '{serviceId}', 100.00, 'VND', '2026-01-01T00:00:00Z', '{actorId}');
            """);

        await AssertSqlStateAsync(
            PostgresErrorCodes.ExclusionViolation,
            () => ExecuteAsync(
                connection,
                $"""
                INSERT INTO survey.survey_service_prices
                    (id, survey_service_id, price_per_ha, currency, effective_from, created_by)
                VALUES
                    ('{Guid.NewGuid()}', '{serviceId}', 120.00, 'VND', '2026-06-01T00:00:00Z', '{actorId}');
                """));

        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO survey.survey_requests
                (id, request_number, kind, tenant_id, farm_id, requested_by_user_id,
                 survey_service_id, caller_scope, idempotency_key, applicant_name,
                 applicant_email, applicant_phone, farm_name, farm_address,
                 approximate_area_ha, map_location, status)
            VALUES
                ('{requestA}', 'REQ-{requestA:N}', 'EXISTING_FARM_SURVEY', '{tenantA}', '{farmA}', '{actorId}',
                 '{serviceId}', 'tenant:{tenantA}', 'idem-{requestA:N}', 'Applicant',
                 'applicant@example.test', '0900000000', 'Farm A', 'Test address',
                 1.2345, ST_SetSRID(ST_MakePoint(106.5, 10.5), 4326), 'SUBMITTED'),
                ('{requestB}', 'REQ-{requestB:N}', 'EXISTING_FARM_SURVEY', '{tenantA}', '{farmA}', '{actorId}',
                 '{serviceId}', 'tenant:{tenantA}', 'idem-{requestB:N}', 'Applicant',
                 'applicant@example.test', '0900000000', 'Farm A', 'Test address',
                 1.0000, ST_SetSRID(ST_MakePoint(106.5, 10.5), 4326), 'SUBMITTED');

            INSERT INTO survey.survey_orders
                (id, order_number, tenant_id, farm_id, survey_request_id, survey_service_id,
                 survey_service_price_id, confirmed_survey_area_ha, price_per_ha_snapshot,
                 currency, final_price, scope_confirmed_by, scope_confirmed_at,
                 requires_baseline_mapping, status)
            VALUES
                ('{orderId}', 'ORD-{orderId:N}', '{tenantA}', '{farmA}', '{requestA}', '{serviceId}',
                 '{priceId}', 1.2345, 100.00, 'VND', 123.45, '{actorId}', NOW(), TRUE,
                 'PENDING_SCOPE_CONFIRMATION');
            """);

        Assert.Equal(
            123.45m,
            await ScalarAsync<decimal>(
                connection,
                $"SELECT final_price FROM survey.survey_orders WHERE id = '{orderId}'"));

        await AssertSqlStateAsync(
            PostgresErrorCodes.ForeignKeyViolation,
            () => ExecuteAsync(
                connection,
                $"UPDATE survey.survey_service_prices SET price_per_ha = 101.00 WHERE id = '{priceId}'"));

        await AssertSqlStateAsync(
            PostgresErrorCodes.ForeignKeyViolation,
            () => ExecuteAsync(
                connection,
                $"""
                INSERT INTO survey.survey_orders
                    (id, order_number, tenant_id, farm_id, survey_request_id, survey_service_id,
                     requires_baseline_mapping, status)
                VALUES
                    ('{Guid.NewGuid()}', 'ORD-X-{requestB.ToString("N")[..20]}', '{tenantA}', '{farmB}', '{requestB}',
                     '{serviceId}', FALSE, 'PENDING_SCOPE_CONFIRMATION');
                """));

        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO survey.survey_appointments
                (id, survey_order_id, proposed_start_at, proposed_end_at, status)
            VALUES
                ('{Guid.NewGuid()}', '{orderId}', '2026-10-01T01:00:00Z', '2026-10-01T02:00:00Z', 'PROPOSED');
            """);

        await AssertSqlStateAsync(
            PostgresErrorCodes.UniqueViolation,
            () => ExecuteAsync(
                connection,
                $"""
                INSERT INTO survey.survey_appointments
                    (id, survey_order_id, proposed_start_at, proposed_end_at, status)
                VALUES
                    ('{Guid.NewGuid()}', '{orderId}', '2026-10-02T01:00:00Z', '2026-10-02T02:00:00Z', 'PROPOSED');
                """));

        var originalXmin = await ScalarAsync<string>(
            connection,
            $"SELECT xmin::text FROM survey.survey_services WHERE id = '{serviceId}'");
        await ExecuteAsync(
            connection,
            $"UPDATE survey.survey_services SET updated_at = NOW() WHERE id = '{serviceId}'");
        Assert.Equal(
            0,
            await ExecuteAsync(
                connection,
                $"UPDATE survey.survey_services SET updated_at = NOW() WHERE id = '{serviceId}' AND xmin::text = '{originalXmin}'"));
    }

    private static async Task AssertSqlStateAsync(
        string expectedSqlState,
        Func<Task<int>> operation)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(operation);
        Assert.Equal(expectedSqlState, exception.SqlState);
    }

    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection connection,
        string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var value = await command.ExecuteScalarAsync();
        return Assert.IsType<T>(value);
    }

    private static async Task<int> ExecuteAsync(
        NpgsqlConnection connection,
        string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task RecreateDatabaseAsync(string databaseName)
    {
        await DropDatabaseAsync(databaseName);
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {databaseName}";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    private static string NewDatabaseName(string suffix) =>
        $"agridrone_phase5_{suffix}_{Guid.NewGuid():N}"[..45];

    private static string BuildConnectionString(string databaseName) =>
        $"Host=127.0.0.1;Port=55432;Database={databaseName};Username=agridrone_test;Password=agridrone_test";
}
