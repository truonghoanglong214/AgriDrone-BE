using AgriDrone.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class Step0RPerPoleMigrationIntegrationTests
{
    private static readonly string[] AppointmentPurposeLabels =
        ["BASELINE_MAPPING", "PAID_SERVICE"];

    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";

    private const string Phase5Migration =
        "20260925100711_Phase5SurveyDatabaseFoundation";

    private const string Step0RMigration =
        "20261007120000_RebaselinePerPoleOrder";

    [Fact]
    public async Task FreshAndPhase5UpgradePreserveLegacyAndEnforcePerPoleSchema()
    {
        var freshDatabase = NewDatabaseName("fresh");
        var upgradeDatabase = NewDatabaseName("upgrade");

        try
        {
            await RecreateDatabaseAsync(freshDatabase);
            await using (var freshContext = CreateContext(freshDatabase))
            {
                await freshContext.Database.MigrateAsync(Step0RMigration);
                await AssertStep0RSchemaAsync(freshDatabase);
                await AssertPerPoleConstraintBehaviorAsync(freshDatabase);

                var rollbackException = await Record.ExceptionAsync(() =>
                    freshContext.Database.MigrateAsync(Phase5Migration));
                var postgresException = FindPostgresException(rollbackException);
                Assert.NotNull(postgresException);
                Assert.Equal("P0001", postgresException.SqlState);
                Assert.Equal(
                    Step0RMigration,
                    await LatestMigrationAsync(freshDatabase));
            }

            await RecreateDatabaseAsync(upgradeDatabase);
            await using (var upgradeContext = CreateContext(upgradeDatabase))
            {
                await upgradeContext.Database.MigrateAsync(Phase5Migration);
                var legacy = await SeedLegacyPhase5DataAsync(upgradeDatabase);

                await upgradeContext.Database.MigrateAsync(Step0RMigration);
                await AssertStep0RSchemaAsync(upgradeDatabase);
                await AssertLegacyDataPreservedAsync(upgradeDatabase, legacy);

                await upgradeContext.Database.MigrateAsync(Phase5Migration);
                await AssertPhase5RollbackAsync(upgradeDatabase, legacy.OrderId);

                await upgradeContext.Database.MigrateAsync(Step0RMigration);
                await AssertStep0RSchemaAsync(upgradeDatabase);
                await AssertLegacyDataPreservedAsync(upgradeDatabase, legacy);
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
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(
            BuildConnectionString(databaseName));
        PostgreSqlEnumMappings.ConfigureDataSource(dataSourceBuilder);
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
        AgriDroneSchemaDbContextOptions.Configure(
            options,
            dataSourceBuilder.Build());
        return new AgriDroneSchemaDbContext(options.Options);
    }

    private static async Task AssertStep0RSchemaAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            BuildConnectionString(databaseName));
        await connection.OpenAsync();

        Assert.Equal(Step0RMigration, await LatestMigrationAsync(connection));
        Assert.Equal(
            1L,
            await ScalarAsync<long>(
                connection,
                """
                SELECT count(*)
                FROM information_schema.columns
                WHERE table_schema = 'survey'
                  AND table_name = 'survey_service_prices'
                  AND column_name = 'price_per_pole'
                  AND data_type = 'numeric'
                """));
        Assert.Equal(
            "YES",
            await ScalarAsync<string>(
                connection,
                """
                SELECT is_nullable
                FROM information_schema.columns
                WHERE table_schema = 'survey'
                  AND table_name = 'survey_service_prices'
                  AND column_name = 'price_per_ha'
                """));
        Assert.Equal(
            3L,
            await ScalarAsync<long>(
                connection,
                """
                SELECT count(*)
                FROM pg_constraint
                WHERE conname IN
                (
                    'ck_survey_service_prices_pricing_mode',
                    'ck_survey_orders_pole_count_snapshot_complete',
                    'ck_survey_orders_per_pole_pricing_snapshot_complete'
                )
                """));
        Assert.Equal(
            1L,
            await ScalarAsync<long>(
                connection,
                """
                SELECT count(*)
                FROM pg_indexes
                WHERE schemaname = 'survey'
                  AND indexname = 'uq_survey_appointments_one_active_per_order_purpose'
                """));
        Assert.Equal(
            AppointmentPurposeLabels,
            await EnumLabelsAsync(connection, "survey_appointment_purpose"));
        Assert.Contains(
            "AWAITING_PRICING",
            await EnumLabelsAsync(connection, "survey_order_status"));
    }

    private static async Task AssertPerPoleConstraintBehaviorAsync(
        string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            BuildConnectionString(databaseName));
        await connection.OpenAsync();

        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var priceId = Guid.NewGuid();
        var mapId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();
        const string serviceId = "10000000-0000-0000-0000-000000000001";

        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO identity.users (id, email, password_hash, full_name)
            VALUES ('{actorId}', 'step0r-{actorId:N}@example.test', 'not-used', 'Step 0R Actor');

            INSERT INTO identity.tenants (id, code, name)
            VALUES ('{tenantId}', 'S0R-{tenantId:N}', 'Step 0R Tenant');

            INSERT INTO farm.farms (id, tenant_id, code, name, created_by)
            VALUES ('{farmId}', '{tenantId}', 'S0R-F', 'Step 0R Farm', '{actorId}');

            INSERT INTO survey.survey_service_prices
                (id, survey_service_id, price_per_pole, currency, effective_from, created_by)
            VALUES
                ('{priceId}', '{serviceId}', 10000.00, 'VND', '2026-10-01T00:00:00Z', '{actorId}');

            INSERT INTO survey.survey_requests
                (id, request_number, kind, tenant_id, farm_id, requested_by_user_id,
                 survey_service_id, caller_scope, idempotency_key, applicant_name,
                 applicant_email, applicant_phone, farm_name, farm_address,
                 approximate_area_ha, map_location, status)
            VALUES
                ('{requestId}', 'REQ-{requestId:N}', 'EXISTING_FARM_SURVEY', '{tenantId}', '{farmId}', '{actorId}',
                 '{serviceId}', 'tenant:{tenantId}', 'idem-{requestId:N}', 'Applicant',
                 'applicant@example.test', '0900000000', 'Step 0R Farm', 'Test address',
                 1.0000, ST_SetSRID(ST_MakePoint(106.5, 10.5), 4326), 'SUBMITTED');

            INSERT INTO survey.survey_orders
                (id, order_number, tenant_id, farm_id, survey_request_id,
                 survey_service_id, requires_baseline_mapping, status)
            VALUES
                ('{orderId}', 'ORD-{orderId:N}', '{tenantId}', '{farmId}', '{requestId}',
                 '{serviceId}', FALSE, 'PENDING_BOUNDARY_VERIFICATION');

            INSERT INTO farm.farm_base_map_versions
                (id, tenant_id, farm_id, version_number, source_survey_order_id,
                 status, published_by, published_at)
            VALUES
                ('{mapId}', '{tenantId}', '{farmId}', 1, '{orderId}',
                 'PUBLISHED', '{actorId}', '2026-10-07T00:00:00Z');

            UPDATE survey.survey_orders
            SET survey_service_price_id = '{priceId}',
                confirmed_survey_pole_count = 125,
                price_per_pole_snapshot = 10000.00,
                farm_boundary_version_id = '{boundaryId}',
                farm_base_map_version_id = '{mapId}',
                pole_count_confirmed_by = '{actorId}',
                pole_count_confirmed_at = '2026-10-07T00:00:00Z',
                pricing_confirmed_by = '{actorId}',
                pricing_confirmed_at = '2026-10-07T00:01:00Z',
                scope_confirmed_by = '{actorId}',
                scope_confirmed_at = '2026-10-07T00:00:00Z',
                currency = 'VND',
                final_price = 1250000.00,
                status = 'AWAITING_PAID_APPOINTMENT'
            WHERE id = '{orderId}';

            INSERT INTO survey.survey_appointments
                (id, survey_order_id, purpose, proposed_start_at, proposed_end_at, status)
            VALUES
                ('{Guid.NewGuid()}', '{orderId}', 'BASELINE_MAPPING', '2026-10-08T01:00:00Z', '2026-10-08T02:00:00Z', 'PROPOSED'),
                ('{Guid.NewGuid()}', '{orderId}', 'PAID_SERVICE', '2026-10-09T01:00:00Z', '2026-10-09T02:00:00Z', 'PROPOSED');
            """);

        Assert.Equal(
            1_250_000m,
            await ScalarAsync<decimal>(
                connection,
                $"SELECT final_price FROM survey.survey_orders WHERE id = '{orderId}'"));

        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(
                connection,
                $"UPDATE survey.survey_orders SET final_price = 1.00 WHERE id = '{orderId}'"));
        await AssertSqlStateAsync(
            PostgresErrorCodes.UniqueViolation,
            () => ExecuteAsync(
                connection,
                $"""
                INSERT INTO survey.survey_appointments
                    (id, survey_order_id, purpose, proposed_start_at, proposed_end_at, status)
                VALUES
                    ('{Guid.NewGuid()}', '{orderId}', 'PAID_SERVICE', '2026-10-10T01:00:00Z', '2026-10-10T02:00:00Z', 'PROPOSED')
                """));
    }

    private static async Task<LegacyData> SeedLegacyPhase5DataAsync(
        string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            BuildConnectionString(databaseName));
        await connection.OpenAsync();

        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var priceId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        const string serviceId = "10000000-0000-0000-0000-000000000002";

        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO identity.users (id, email, password_hash, full_name)
            VALUES ('{actorId}', 'legacy-{actorId:N}@example.test', 'not-used', 'Legacy Actor');
            INSERT INTO identity.tenants (id, code, name)
            VALUES ('{tenantId}', 'LEG-{tenantId:N}', 'Legacy Tenant');
            INSERT INTO farm.farms (id, tenant_id, code, name, created_by)
            VALUES ('{farmId}', '{tenantId}', 'LEG-F', 'Legacy Farm', '{actorId}');
            INSERT INTO survey.survey_service_prices
                (id, survey_service_id, price_per_ha, currency, effective_from, created_by)
            VALUES
                ('{priceId}', '{serviceId}', 250000.00, 'VND', '2026-01-01T00:00:00Z', '{actorId}');
            INSERT INTO survey.survey_requests
                (id, request_number, kind, tenant_id, farm_id, requested_by_user_id,
                 survey_service_id, caller_scope, idempotency_key, applicant_name,
                 applicant_email, applicant_phone, farm_name, farm_address,
                 approximate_area_ha, map_location, status)
            VALUES
                ('{requestId}', 'REQ-{requestId:N}', 'EXISTING_FARM_SURVEY', '{tenantId}', '{farmId}', '{actorId}',
                 '{serviceId}', 'tenant:{tenantId}', 'idem-{requestId:N}', 'Applicant',
                 'applicant@example.test', '0900000000', 'Legacy Farm', 'Test address',
                 2.0000, ST_SetSRID(ST_MakePoint(106.5, 10.5), 4326), 'SUBMITTED');
            INSERT INTO survey.survey_orders
                (id, order_number, tenant_id, farm_id, survey_request_id, survey_service_id,
                 survey_service_price_id, confirmed_survey_area_ha, price_per_ha_snapshot,
                 currency, final_price, scope_confirmed_by, scope_confirmed_at,
                 requires_baseline_mapping, status)
            VALUES
                ('{orderId}', 'ORD-{orderId:N}', '{tenantId}', '{farmId}', '{requestId}', '{serviceId}',
                 '{priceId}', 2.0000, 250000.00, 'VND', 500000.00, '{actorId}', NOW(), FALSE,
                 'AWAITING_APPOINTMENT');
            INSERT INTO survey.survey_appointments
                (id, survey_order_id, proposed_start_at, proposed_end_at, status)
            VALUES
                ('{appointmentId}', '{orderId}', '2026-10-08T01:00:00Z', '2026-10-08T02:00:00Z', 'PROPOSED');
            """);

        return new LegacyData(orderId, priceId, appointmentId);
    }

    private static async Task AssertLegacyDataPreservedAsync(
        string databaseName,
        LegacyData legacy)
    {
        await using var connection = new NpgsqlConnection(
            BuildConnectionString(databaseName));
        await connection.OpenAsync();

        Assert.Equal(
            250_000m,
            await ScalarAsync<decimal>(
                connection,
                $"SELECT price_per_ha FROM survey.survey_service_prices WHERE id = '{legacy.PriceId}'"));
        Assert.Equal(
            0L,
            await ScalarAsync<long>(
                connection,
                $"SELECT count(*) FROM survey.survey_service_prices WHERE id = '{legacy.PriceId}' AND price_per_pole IS NOT NULL"));
        Assert.Equal(
            "AWAITING_PAID_APPOINTMENT",
            await ScalarAsync<string>(
                connection,
                $"SELECT status::text FROM survey.survey_orders WHERE id = '{legacy.OrderId}'"));
        Assert.Equal(
            "PAID_SERVICE",
            await ScalarAsync<string>(
                connection,
                $"SELECT purpose::text FROM survey.survey_appointments WHERE id = '{legacy.AppointmentId}'"));
    }

    private static async Task AssertPhase5RollbackAsync(
        string databaseName,
        Guid orderId)
    {
        await using var connection = new NpgsqlConnection(
            BuildConnectionString(databaseName));
        await connection.OpenAsync();

        Assert.Equal(Phase5Migration, await LatestMigrationAsync(connection));
        Assert.Equal(
            "AWAITING_APPOINTMENT",
            await ScalarAsync<string>(
                connection,
                $"SELECT status::text FROM survey.survey_orders WHERE id = '{orderId}'"));
        Assert.Equal(
            0L,
            await ScalarAsync<long>(
                connection,
                """
                SELECT count(*)
                FROM information_schema.columns
                WHERE table_schema = 'survey'
                  AND table_name = 'survey_service_prices'
                  AND column_name = 'price_per_pole'
                """));
    }

    private static async Task AssertSqlStateAsync(
        string expectedSqlState,
        Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(action);
        Assert.Equal(expectedSqlState, exception.SqlState);
    }

    private static PostgresException? FindPostgresException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is PostgresException postgresException)
            {
                return postgresException;
            }

            exception = exception.InnerException;
        }

        return null;
    }

    private static async Task<string[]> EnumLabelsAsync(
        NpgsqlConnection connection,
        string typeName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT enumlabel
            FROM pg_enum
            INNER JOIN pg_type ON pg_type.oid = pg_enum.enumtypid
            INNER JOIN pg_namespace ON pg_namespace.oid = pg_type.typnamespace
            WHERE pg_namespace.nspname = 'system'
              AND pg_type.typname = @type_name
            ORDER BY enumsortorder
            """;
        command.Parameters.AddWithValue("type_name", typeName);
        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values.ToArray();
    }

    private static async Task<string> LatestMigrationAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(
            BuildConnectionString(databaseName));
        await connection.OpenAsync();
        return await LatestMigrationAsync(connection);
    }

    private static Task<string> LatestMigrationAsync(NpgsqlConnection connection) =>
        ScalarAsync<string>(
            connection,
            "SELECT \"MigrationId\" FROM system.__ef_migrations_history ORDER BY \"MigrationId\" DESC LIMIT 1");

    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection connection,
        string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return Assert.IsType<T>(result);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql)
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
        $"agridrone_step0r_{suffix}_{Guid.NewGuid():N}";

    private sealed record LegacyData(
        Guid OrderId,
        Guid PriceId,
        Guid AppointmentId);
}
