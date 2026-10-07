using AgriDrone.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class Step0RFarmBoundaryMigrationIntegrationTests
{
    private static readonly string[] FarmBoundaryStatusLabels =
        ["DRAFT", "APPROVED", "REJECTED", "SUPERSEDED"];

    private static readonly string[] BoundaryExceptionStateLabels =
        ["OUT_OF_BOUNDARY", "NEEDS_REVIEW", "RESOLVED"];

    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";

    private const string PreviousMigration =
        "20261007120000_RebaselinePerPoleOrder";

    private const string BoundaryMigration =
        "20261007140000_AddVersionedFarmBoundaries";

    [Fact]
    public async Task FreshUpgradeAndGuardedRollbackEnforceBoundaryContract()
    {
        var freshDatabase = NewDatabaseName("fresh");
        var upgradeDatabase = NewDatabaseName("upgrade");

        try
        {
            await RecreateDatabaseAsync(freshDatabase);
            await using (var freshContext = CreateContext(freshDatabase))
            {
                await freshContext.Database.MigrateAsync(BoundaryMigration);
                await AssertSchemaAsync(freshDatabase);
                await AssertDatabaseInvariantsAsync(freshDatabase);
            }

            await RecreateDatabaseAsync(upgradeDatabase);
            await using (var upgradeContext = CreateContext(upgradeDatabase))
            {
                await upgradeContext.Database.MigrateAsync(PreviousMigration);
                var legacy = await SeedLegacyBoundaryAsync(upgradeDatabase);

                await upgradeContext.Database.MigrateAsync(BoundaryMigration);
                await AssertSchemaAsync(upgradeDatabase);
                await AssertLegacyDraftAsync(upgradeDatabase, legacy);

                await upgradeContext.Database.MigrateAsync(PreviousMigration);
                await AssertRolledBackAsync(upgradeDatabase, legacy.FarmId);

                await upgradeContext.Database.MigrateAsync(BoundaryMigration);
                await ApproveLegacyDraftAsync(upgradeDatabase, legacy);

                var rollbackException = await Record.ExceptionAsync(() =>
                    upgradeContext.Database.MigrateAsync(PreviousMigration));
                var postgresException = FindPostgresException(rollbackException);
                Assert.NotNull(postgresException);
                Assert.Equal("P0001", postgresException.SqlState);
                Assert.Equal(BoundaryMigration, await LatestMigrationAsync(upgradeDatabase));
            }
        }
        finally
        {
            await DropDatabaseAsync(freshDatabase);
            await DropDatabaseAsync(upgradeDatabase);
        }
    }

    private static async Task AssertSchemaAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();

        Assert.Equal(BoundaryMigration, await LatestMigrationAsync(connection));
        Assert.Equal(
            2L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'farm' AND table_name IN ('farm_boundaries', 'boundary_exceptions')"));
        Assert.Equal(
            4L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM pg_trigger WHERE tgname IN ('tr_farm_boundaries_protect_history', 'tr_farm_boundaries_validate_zones', 'tr_farm_zones_enforce_approved_boundary', 'tr_boundary_exceptions_protect_history') AND NOT tgisinternal"));
        Assert.Equal(
            1L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM pg_constraint WHERE conname = 'fk_survey_orders_farm_boundary_same_tenant_farm'"));
        Assert.Equal(
            FarmBoundaryStatusLabels,
            await EnumLabelsAsync(connection, "farm_boundary_status"));
        Assert.Equal(
            BoundaryExceptionStateLabels,
            await EnumLabelsAsync(connection, "boundary_exception_state"));
    }

    private static async Task AssertDatabaseInvariantsAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();

        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();
        var secondBoundaryId = Guid.NewGuid();
        var boundaryExceptionId = Guid.NewGuid();

        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO identity.users (id, email, password_hash, full_name)
            VALUES ('{actorId}', 'boundary-{actorId:N}@example.test', 'not-used', 'Boundary Reviewer');
            INSERT INTO identity.tenants (id, code, name)
            VALUES ('{tenantId}', 'BND-{tenantId:N}', 'Boundary Tenant');
            INSERT INTO farm.farms (id, tenant_id, code, name, created_by)
            VALUES ('{farmId}', '{tenantId}', 'BND-F', 'Boundary Farm', '{actorId}');
            INSERT INTO farm.farm_zones (id, farm_id, code, name, boundary, status, created_by)
            VALUES
                ('{Guid.NewGuid()}', '{farmId}', 'A', 'Zone A', ST_GeomFromText('POLYGON((1 1,4 1,4 4,1 4,1 1))', 4326), 'ACTIVE', '{actorId}'),
                ('{Guid.NewGuid()}', '{farmId}', 'B', 'Zone B', ST_GeomFromText('POLYGON((4 1,7 1,7 4,4 4,4 1))', 4326), 'ACTIVE', '{actorId}');
            INSERT INTO farm.farm_boundaries
                (id, tenant_id, farm_id, version_number, geometry, source, submitted_by, status)
            VALUES
                ('{boundaryId}', '{tenantId}', '{farmId}', 1, ST_GeomFromText('POLYGON((0 0,10 0,10 10,0 10,0 0))', 4326), 'TENANT_OWNER', '{actorId}', 'DRAFT');
            UPDATE farm.farm_boundaries
            SET status = 'APPROVED', reviewed_by = '{actorId}', reviewed_at = NOW(),
                review_reason = 'Verified', updated_at = NOW()
            WHERE id = '{boundaryId}';
            INSERT INTO farm.farm_boundaries
                (id, tenant_id, farm_id, version_number, geometry, source, submitted_by, status)
            VALUES
                ('{secondBoundaryId}', '{tenantId}', '{farmId}', 2, ST_GeomFromText('POLYGON((0 0,11 0,11 11,0 11,0 0))', 4326), 'TENANT_OWNER', '{actorId}', 'DRAFT');
            """);

        await AssertSqlStateAsync(
            PostgresErrorCodes.UniqueViolation,
            () => ExecuteAsync(
                connection,
                $"""
                UPDATE farm.farm_boundaries
                SET status = 'APPROVED', reviewed_by = '{actorId}', reviewed_at = NOW(),
                    review_reason = 'Second current boundary', updated_at = NOW()
                WHERE id = '{secondBoundaryId}'
                """));

        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(
                connection,
                $"""
                INSERT INTO farm.farm_zones (id, farm_id, code, name, boundary, status, created_by)
                VALUES ('{Guid.NewGuid()}', '{farmId}', 'OUT', 'Outside', ST_GeomFromText('POLYGON((9 9,12 9,12 12,9 12,9 9))', 4326), 'ACTIVE', '{actorId}')
                """));

        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO farm.boundary_exceptions
                (id, tenant_id, farm_id, farm_boundary_version_id, source, source_reference_id,
                 original_position, state, measured_distance_meters, threshold_meters, policy_version)
            VALUES
                ('{boundaryExceptionId}', '{tenantId}', '{farmId}', '{boundaryId}', 'BASELINE_CANDIDATE', 'candidate-1',
                 ST_SetSRID(ST_MakePoint(10.1, 5), 4326), 'NEEDS_REVIEW', 0.100, 1.500, 'boundary-distance-v1');
            """);

        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(
                connection,
                $"UPDATE farm.boundary_exceptions SET original_position = ST_SetSRID(ST_MakePoint(9.9, 5), 4326) WHERE id = '{boundaryExceptionId}'"));

        await ExecuteAsync(
            connection,
            $"""
            UPDATE farm.boundary_exceptions
            SET state = 'RESOLVED', decision = 'LOCATION_CORRECTED',
                corrected_position = ST_SetSRID(ST_MakePoint(9.9, 5), 4326),
                reviewed_by = '{actorId}', reviewed_at = NOW(), review_reason = 'GPS corrected',
                review_evidence = jsonb_build_object('mediaId', 'evidence-1'), updated_at = NOW()
            WHERE id = '{boundaryExceptionId}'
            """);

        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(
                connection,
                $"UPDATE farm.boundary_exceptions SET review_reason = 'Changed history' WHERE id = '{boundaryExceptionId}'"));
    }

    private static async Task<LegacyBoundary> SeedLegacyBoundaryAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();

        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO identity.users (id, email, password_hash, full_name)
            VALUES ('{actorId}', 'legacy-boundary-{actorId:N}@example.test', 'not-used', 'Legacy Boundary Owner');
            INSERT INTO identity.tenants (id, code, name)
            VALUES ('{tenantId}', 'LGB-{tenantId:N}', 'Legacy Boundary Tenant');
            INSERT INTO farm.farms (id, tenant_id, code, name, boundary, created_by)
            VALUES ('{farmId}', '{tenantId}', 'LGB-F', 'Legacy Boundary Farm',
                    ST_GeomFromText('POLYGON((0 0,10 0,10 10,0 10,0 0))', 4326), '{actorId}');
            INSERT INTO farm.farm_zones (id, farm_id, code, name, boundary, status, created_by)
            VALUES ('{Guid.NewGuid()}', '{farmId}', 'LEG-Z', 'Legacy Zone',
                    ST_GeomFromText('POLYGON((1 1,5 1,5 5,1 5,1 1))', 4326), 'ACTIVE', '{actorId}');
            """);

        return new LegacyBoundary(actorId, tenantId, farmId);
    }

    private static async Task AssertLegacyDraftAsync(
        string databaseName,
        LegacyBoundary legacy)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();

        Assert.Equal(
            1L,
            await ScalarAsync<long>(
                connection,
                $"SELECT count(*) FROM farm.farm_boundaries WHERE farm_id = '{legacy.FarmId}' AND tenant_id = '{legacy.TenantId}' AND source = 'LEGACY_IMPORT' AND status = 'DRAFT' AND submitted_by = '{legacy.ActorId}' AND ST_Equals(geometry, (SELECT boundary FROM farm.farms WHERE id = '{legacy.FarmId}'))"));
    }

    private static async Task AssertRolledBackAsync(string databaseName, Guid farmId)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();

        Assert.Equal(PreviousMigration, await LatestMigrationAsync(connection));
        Assert.Equal(
            0L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'farm' AND table_name IN ('farm_boundaries', 'boundary_exceptions')"));
        Assert.Equal(
            1L,
            await ScalarAsync<long>(
                connection,
                $"SELECT count(*) FROM farm.farms WHERE id = '{farmId}' AND boundary IS NOT NULL"));
    }

    private static async Task ApproveLegacyDraftAsync(
        string databaseName,
        LegacyBoundary legacy)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            $"""
            UPDATE farm.farm_boundaries
            SET status = 'APPROVED', reviewed_by = '{legacy.ActorId}', reviewed_at = NOW(),
                review_reason = 'Verified legacy geometry', updated_at = NOW()
            WHERE farm_id = '{legacy.FarmId}'
            """);
    }

    private static AgriDroneSchemaDbContext CreateContext(string databaseName)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(BuildConnectionString(databaseName));
        PostgreSqlEnumMappings.ConfigureDataSource(dataSourceBuilder);
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
        AgriDroneSchemaDbContextOptions.Configure(options, dataSourceBuilder.Build());
        return new AgriDroneSchemaDbContext(options.Options);
    }

    private static async Task AssertSqlStateAsync(string expectedSqlState, Func<Task> action)
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
            JOIN pg_type ON pg_type.oid = pg_enum.enumtypid
            JOIN pg_namespace ON pg_namespace.oid = pg_type.typnamespace
            WHERE pg_namespace.nspname = 'system' AND pg_type.typname = @type_name
            ORDER BY enumsortorder
            """;
        command.Parameters.AddWithValue("type_name", typeName);
        var labels = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            labels.Add(reader.GetString(0));
        }

        return labels.ToArray();
    }

    private static async Task<string> LatestMigrationAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();
        return await LatestMigrationAsync(connection);
    }

    private static Task<string> LatestMigrationAsync(NpgsqlConnection connection) =>
        ScalarAsync<string>(
            connection,
            "SELECT \"MigrationId\" FROM system.__ef_migrations_history ORDER BY \"MigrationId\" DESC LIMIT 1");

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return Assert.IsType<T>(result);
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
        $"agridrone_0r3_{suffix}_{Guid.NewGuid():N}";

    private sealed record LegacyBoundary(Guid ActorId, Guid TenantId, Guid FarmId);
}
