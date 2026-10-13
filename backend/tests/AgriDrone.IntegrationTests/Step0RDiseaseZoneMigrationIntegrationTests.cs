using AgriDrone.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class Step0RDiseaseZoneMigrationIntegrationTests
{
    private static readonly string[] DiseaseZoneStatusLabels =
        ["PROPOSED", "REVIEWED", "PUBLISHED", "REJECTED", "SUPERSEDED"];

    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";

    private const string PreviousMigration =
        "20261007140000_AddVersionedFarmBoundaries";

    private const string DiseaseZoneMigration =
        "20261007160000_AddDiseaseZonesAndRecommendations";

    [Fact]
    public async Task FreshUpgradeRollbackAndDatabaseGuardsEnforceDiseaseZoneContract()
    {
        var freshDatabase = NewDatabaseName("fresh");
        var upgradeDatabase = NewDatabaseName("upgrade");

        try
        {
            await RecreateDatabaseAsync(freshDatabase);
            await using (var freshContext = CreateContext(freshDatabase))
            {
                await freshContext.Database.MigrateAsync();
                await AssertSchemaAsync(freshDatabase);
            }

            await RecreateDatabaseAsync(upgradeDatabase);
            await using (var upgradeContext = CreateContext(upgradeDatabase))
            {
                await upgradeContext.Database.MigrateAsync(PreviousMigration);
                await upgradeContext.Database.MigrateAsync();
                await AssertSchemaAsync(upgradeDatabase);

                await upgradeContext.Database.MigrateAsync(PreviousMigration);
                await AssertRolledBackAsync(upgradeDatabase);

                await upgradeContext.Database.MigrateAsync();
                await AssertDatabaseInvariantsAsync(upgradeDatabase);

                var rollbackException = await Record.ExceptionAsync(() =>
                    upgradeContext.Database.MigrateAsync(PreviousMigration));
                var postgresException = FindPostgresException(rollbackException);
                Assert.NotNull(postgresException);
                Assert.Equal("P0001", postgresException.SqlState);
                Assert.Equal(DiseaseZoneMigration, await LatestMigrationAsync(upgradeDatabase));
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

        Assert.Equal(DiseaseZoneMigration, await LatestMigrationAsync(connection));
        Assert.Equal(
            3L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'plant' AND table_name IN ('disease_zones', 'disease_zone_memberships', 'treatment_recommendations')"));
        Assert.Equal(
            4L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM pg_trigger WHERE tgname IN ('tr_treatment_recommendations_protect_history', 'tr_disease_zones_protect_history', 'tr_disease_zones_validate_decision', 'tr_disease_zone_memberships_protect_history') AND NOT tgisinternal"));
        Assert.Equal(
            DiseaseZoneStatusLabels,
            await EnumLabelsAsync(connection, "disease_zone_status"));
    }

    private static async Task AssertRolledBackAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();

        Assert.Equal(PreviousMigration, await LatestMigrationAsync(connection));
        Assert.Equal(
            0L,
            await ScalarAsync<long>(
                connection,
                "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'plant' AND table_name IN ('disease_zones', 'disease_zone_memberships', 'treatment_recommendations')"));
    }

    private static async Task AssertDatabaseInvariantsAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();

        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var farmBoundaryId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var baseMapId = Guid.NewGuid();
        var resultId = Guid.NewGuid();
        var conditionId = Guid.NewGuid();
        var plantId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var diseaseZoneId = Guid.NewGuid();
        var zoneKey = Guid.NewGuid();
        var handoffId = Guid.NewGuid();
        var outsideDiseaseZoneId = Guid.NewGuid();
        const string serviceId = "10000000-0000-0000-0000-000000000001";

        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO identity.users (id, email, password_hash, full_name)
            VALUES ('{actorId}', 'disease-zone-{actorId:N}@example.test', 'not-used', 'Disease Zone Reviewer');
            INSERT INTO identity.tenants (id, code, name)
            VALUES ('{tenantId}', 'DZ-{tenantId:N}', 'Disease Zone Tenant');
            INSERT INTO farm.farms (id, tenant_id, code, name, created_by)
            VALUES ('{farmId}', '{tenantId}', 'DZ-F', 'Disease Zone Farm', '{actorId}');
            INSERT INTO farm.farm_boundaries
                (id, tenant_id, farm_id, version_number, geometry, source, submitted_by,
                 status, reviewed_by, reviewed_at, review_reason)
            VALUES
                ('{farmBoundaryId}', '{tenantId}', '{farmId}', 1,
                 ST_GeomFromText('POLYGON((0 0,20 0,20 20,0 20,0 0))', 4326),
                 'TENANT_OWNER', '{actorId}', 'APPROVED', '{actorId}', NOW(), 'Verified boundary');
            INSERT INTO farm.farm_zones (id, farm_id, code, name, boundary, status, created_by)
            VALUES ('{zoneId}', '{farmId}', 'DZ-A', 'Disease Zone A',
                    ST_GeomFromText('POLYGON((1 1,10 1,10 10,1 10,1 1))', 4326), 'ACTIVE', '{actorId}');
            INSERT INTO survey.survey_requests
                (id, request_number, kind, tenant_id, farm_id, requested_by_user_id,
                 survey_service_id, caller_scope, idempotency_key, applicant_name,
                 applicant_email, applicant_phone, farm_name, farm_address,
                 approximate_area_ha, map_location, status)
            VALUES
                ('{requestId}', 'REQ-{requestId:N}', 'EXISTING_FARM_SURVEY', '{tenantId}', '{farmId}', '{actorId}',
                 '{serviceId}', 'tenant:{tenantId}', 'idem-{requestId:N}', 'Applicant',
                 'applicant@example.test', '0900000000', 'Disease Zone Farm', 'Test address',
                 1.0000, ST_SetSRID(ST_MakePoint(5, 5), 4326), 'SUBMITTED');
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
                ('{baseMapId}', '{tenantId}', '{farmId}', 1, '{orderId}',
                 'PUBLISHED', '{actorId}', NOW());
            INSERT INTO survey.survey_results
                (id, survey_order_id, tenant_id, farm_id, service_type, status, provenance)
            VALUES
                ('{resultId}', '{orderId}', '{tenantId}', '{farmId}', 'PLANT_HEALTH', 'PENDING_REVIEW', jsonb_build_object());
            INSERT INTO plant.plant_conditions
                (id, code, name, condition_type, revision_number, is_active)
            VALUES
                ('{conditionId}', 'DZ-FUNGAL', 'Fungal condition', 'DISEASE', 1, TRUE);
            INSERT INTO plant.plants
                (id, farm_id, zone_id, plant_code, location, lifecycle_status,
                 position_source, current_health_level_id)
            SELECT
                '{plantId}', '{farmId}', '{zoneId}', 'PLANT-DZ-1',
                ST_SetSRID(ST_MakePoint(4, 4), 4326), 'ACTIVE', 'MANUAL', id
            FROM plant.health_levels WHERE code = 'MODERATE';
            INSERT INTO plant.treatment_recommendations
                (id, code, version_number, plant_condition_id, health_level_id,
                 title, guidance, advisory_disclaimer, expert_source, source_reference,
                 effective_from, effective_to, status, created_by)
            SELECT
                '{recommendationId}', 'REC-DZ-FUNGAL', 1, '{conditionId}', id,
                'Validated recommendation', 'Follow validated protocol.',
                'Advisory only.', 'Agronomy Board', 'AGR-2026-10',
                '2026-01-01T00:00:00Z', '2027-01-01T00:00:00Z', 'DRAFT', '{actorId}'
            FROM plant.health_levels WHERE code = 'MODERATE';
            UPDATE plant.treatment_recommendations
            SET status = 'PUBLISHED', published_by = '{actorId}', published_at = NOW(), updated_at = NOW()
            WHERE id = '{recommendationId}';
            INSERT INTO plant.disease_zones
                (id, zone_key, version_number, tenant_id, farm_id, survey_order_id,
                 survey_result_id, farm_boundary_version_id, farm_base_map_version_id,
                 plant_condition_id, health_level_id, source_handoff_id, source_proposal_id,
                 proposed_geometry, recommendation_candidates, proposal_evidence,
                 current_membership_version, status)
            SELECT
                '{diseaseZoneId}', '{zoneKey}', 1, '{tenantId}', '{farmId}', '{orderId}',
                '{resultId}', '{farmBoundaryId}', '{baseMapId}', '{conditionId}', id,
                '{handoffId}', 'proposal-1',
                ST_GeomFromText('POLYGON((2 2,7 2,7 7,2 7,2 2))', 4326),
                jsonb_build_array('{recommendationId}'::text), jsonb_build_object('source', 'ai'),
                1, 'PROPOSED'
            FROM plant.health_levels WHERE code = 'MODERATE';
            INSERT INTO plant.disease_zone_memberships
                (id, disease_zone_id, farm_id, plant_id, membership_version, kind, confidence)
            VALUES
                ('{Guid.NewGuid()}', '{diseaseZoneId}', '{farmId}', '{plantId}', 1, 'PROPOSED', 0.9500),
                ('{Guid.NewGuid()}', '{diseaseZoneId}', '{farmId}', '{plantId}', 2, 'REVIEWED', NULL);
            UPDATE plant.disease_zones
            SET reviewed_geometry = ST_GeomFromText('POLYGON((2 2,6 2,6 6,2 6,2 2))', 4326),
                current_membership_version = 2,
                selected_treatment_recommendation_id = '{recommendationId}',
                reviewed_by = '{actorId}', reviewed_at = NOW(),
                review_reason = 'Field verified', review_evidence = jsonb_build_object('mediaId', 'evidence-1'),
                status = 'REVIEWED', updated_at = NOW()
            WHERE id = '{diseaseZoneId}';
            UPDATE plant.disease_zones
            SET status = 'PUBLISHED', published_by = '{actorId}', published_at = NOW(), updated_at = NOW()
            WHERE id = '{diseaseZoneId}';
            """);

        Assert.Equal(
            "PUBLISHED",
            await ScalarAsync<string>(
                connection,
                $"SELECT status::text FROM plant.disease_zones WHERE id = '{diseaseZoneId}'"));

        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(
                connection,
                $"UPDATE plant.disease_zones SET proposed_geometry = ST_GeomFromText('POLYGON((3 3,8 3,8 8,3 8,3 3))', 4326) WHERE id = '{diseaseZoneId}'"));

        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(
                connection,
                $"UPDATE plant.treatment_recommendations SET guidance = 'Unreviewed free-form advice' WHERE id = '{recommendationId}'"));

        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(
                connection,
                $"UPDATE plant.disease_zone_memberships SET confidence = 0.1000 WHERE disease_zone_id = '{diseaseZoneId}' AND membership_version = 1"));

        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO plant.disease_zones
                (id, zone_key, version_number, tenant_id, farm_id, survey_order_id,
                 survey_result_id, farm_boundary_version_id, farm_base_map_version_id,
                 plant_condition_id, health_level_id, source_handoff_id, source_proposal_id,
                 proposed_geometry, recommendation_candidates, proposal_evidence,
                 current_membership_version, status)
            SELECT
                '{outsideDiseaseZoneId}', '{Guid.NewGuid()}', 1, '{tenantId}', '{farmId}', '{orderId}',
                '{resultId}', '{farmBoundaryId}', '{baseMapId}', '{conditionId}', id,
                '{Guid.NewGuid()}', 'proposal-outside',
                ST_GeomFromText('POLYGON((3 3,5 3,5 5,3 5,3 3))', 4326),
                jsonb_build_array('{recommendationId}'::text), jsonb_build_object('source', 'ai'),
                1, 'PROPOSED'
            FROM plant.health_levels WHERE code = 'MODERATE';
            INSERT INTO plant.disease_zone_memberships
                (id, disease_zone_id, farm_id, plant_id, membership_version, kind)
            VALUES
                ('{Guid.NewGuid()}', '{outsideDiseaseZoneId}', '{farmId}', '{plantId}', 1, 'PROPOSED'),
                ('{Guid.NewGuid()}', '{outsideDiseaseZoneId}', '{farmId}', '{plantId}', 2, 'REVIEWED');
            """);

        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(
                connection,
                $"""
                UPDATE plant.disease_zones
                SET reviewed_geometry = ST_GeomFromText('POLYGON((19 19,22 19,22 22,19 22,19 19))', 4326),
                    current_membership_version = 2,
                    selected_treatment_recommendation_id = '{recommendationId}',
                    reviewed_by = '{actorId}', reviewed_at = NOW(),
                    review_reason = 'Invalid outside review', review_evidence = jsonb_build_object(),
                    status = 'REVIEWED', updated_at = NOW()
                WHERE id = '{outsideDiseaseZoneId}'
                """));
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

    private static async Task<string[]> EnumLabelsAsync(NpgsqlConnection connection, string typeName)
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
        ScalarAsync<string>(connection, "SELECT \"MigrationId\" FROM system.__ef_migrations_history ORDER BY \"MigrationId\" DESC LIMIT 1");

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
        $"agridrone_0r4_{suffix}_{Guid.NewGuid():N}";
}
