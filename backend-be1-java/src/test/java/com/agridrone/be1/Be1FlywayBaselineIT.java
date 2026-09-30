package com.agridrone.be1;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.sql.Connection;
import java.sql.DriverManager;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Statement;
import java.util.Set;
import java.util.UUID;
import org.flywaydb.core.Flyway;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;
import org.testcontainers.containers.PostgreSQLContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;
import org.testcontainers.utility.DockerImageName;

@Testcontainers(disabledWithoutDocker = true)
class Be1FlywayBaselineIT {

    private static final Set<String> BE1_SCHEMAS = Set.of(
            "identity", "farm", "plant", "survey", "notification", "audit", "messaging");

    @Container
    static final PostgreSQLContainer<?> POSTGRES = new PostgreSQLContainer<>(
            DockerImageName.parse("postgis/postgis:17-3.5")
                    .asCompatibleSubstituteFor("postgres"));

    private static Flyway flyway;

    @BeforeAll
    static void migrateFreshDatabase() {
        flyway = Flyway.configure()
                .dataSource(POSTGRES.getJdbcUrl(), POSTGRES.getUsername(), POSTGRES.getPassword())
                .locations("classpath:db/migration")
                .target("1")
                .validateOnMigrate(true)
                .load();

        assertThat(flyway.migrate().migrationsExecuted).isEqualTo(1);
    }

    @Test
    void freshBaselineIsCompleteValidAndContainsNoLegacyOrBe2Tables() throws Exception {
        assertThat(flyway.validateWithResult().validationSuccessful).isTrue();

        try (var connection = connection()) {
            assertThat(queryInt(connection, """
                    SELECT count(*)
                    FROM information_schema.tables
                    WHERE table_type = 'BASE TABLE'
                      AND table_schema IN ('identity','farm','plant','survey','notification','audit','messaging')
                    """)).isEqualTo(34);

            assertThat(queryInt(connection, "SELECT count(*) FROM identity.roles"))
                    .isEqualTo(2);
            assertThat(queryInt(connection, """
                    SELECT count(*)
                    FROM identity.roles
                    WHERE (id = '00000000-0000-0000-0000-000000000001'
                               AND code = 'SYSTEM_ADMIN')
                       OR (id = '00000000-0000-0000-0000-000000000002'
                               AND code = 'SYSTEM_MANAGER')
                    """))
                    .isEqualTo(2);
            assertThat(queryInt(connection, "SELECT count(*) FROM survey.survey_services"))
                    .isEqualTo(2);
            assertThat(queryInt(connection, "SELECT count(*) FROM plant.health_levels"))
                    .isEqualTo(5);
            assertThat(queryInt(connection, "SELECT count(*) FROM plant.plant_conditions"))
                    .isEqualTo(4);

            assertThat(queryInt(connection, """
                    SELECT count(*) FROM information_schema.tables
                    WHERE table_schema IN ('field_task','harvest','mission','media')
                       OR table_name IN ('farm_memberships','zone_assignments','plant_scans',
                                         'plant_scan_media','scan_verifications','condition_detections',
                                         'condition_lesions','condition_detection_reviews')
                    """)).isZero();
        }
    }

    @Test
    void geometryContainmentAndNonOverlapAreEnforced() throws Exception {
        UUID tenantId = UUID.randomUUID();
        UUID farmId = UUID.randomUUID();
        try (var connection = connection(); var statement = connection.createStatement()) {
            statement.execute("INSERT INTO identity.tenants(id, code, name) VALUES ('%s', '%s', 'Geometry tenant')"
                    .formatted(tenantId, "T-" + tenantId));
            statement.execute("""
                    INSERT INTO farm.farms(id, tenant_id, code, name, boundary)
                    VALUES ('%s', '%s', 'F-1', 'Geometry farm',
                            ST_GeomFromText('POLYGON((0 0,0 10,10 10,10 0,0 0))', 4326))
                    """.formatted(farmId, tenantId));
            statement.execute("""
                    INSERT INTO farm.farm_zones(id, farm_id, code, name, boundary)
                    VALUES ('%s', '%s', 'Z-1', 'First zone',
                            ST_GeomFromText('POLYGON((1 1,1 4,4 4,4 1,1 1))', 4326))
                    """.formatted(UUID.randomUUID(), farmId));

            assertSqlState(statement, """
                    INSERT INTO farm.farm_zones(id, farm_id, code, name, boundary)
                    VALUES ('%s', '%s', 'Z-OUT', 'Outside zone',
                            ST_GeomFromText('POLYGON((20 20,20 21,21 21,21 20,20 20))', 4326))
                    """.formatted(UUID.randomUUID(), farmId), "23514");

            assertSqlState(statement, """
                    INSERT INTO farm.farm_zones(id, farm_id, code, name, boundary)
                    VALUES ('%s', '%s', 'Z-OVERLAP', 'Overlapping zone',
                            ST_GeomFromText('POLYGON((3 3,3 6,6 6,6 3,3 3))', 4326))
                    """.formatted(UUID.randomUUID(), farmId), "23P01");

            assertSqlState(statement, """
                    INSERT INTO farm.farm_zones(id, farm_id, code, name, boundary)
                    VALUES ('%s', '%s', 'Z-NESTED', 'Nested zone',
                            ST_GeomFromText('POLYGON((2 2,2 3,3 3,3 2,2 2))', 4326))
                    """.formatted(UUID.randomUUID(), farmId), "23P01");
        }
    }

    @Test
    void moneyWindowsAssignmentsAndOptimisticVersionAreEnforced() throws Exception {
        UUID adminId = UUID.randomUUID();
        UUID managerId = UUID.randomUUID();
        UUID tenantId = UUID.randomUUID();
        UUID farmId = UUID.randomUUID();
        UUID profileId = UUID.randomUUID();
        UUID serviceId = UUID.fromString("10000000-0000-0000-0000-000000000001");

        try (var connection = connection(); var statement = connection.createStatement()) {
            statement.execute("INSERT INTO identity.users(id,email,password_hash,full_name) VALUES ('%s','%s@example.test','hash','Admin')"
                    .formatted(adminId, adminId));
            statement.execute("INSERT INTO identity.users(id,email,password_hash,full_name) VALUES ('%s','%s@example.test','hash','Manager')"
                    .formatted(managerId, managerId));
            statement.execute("INSERT INTO identity.tenants(id,code,name) VALUES ('%s','%s','Invariant tenant')"
                    .formatted(tenantId, "T-" + tenantId));
            statement.execute("INSERT INTO farm.farms(id,tenant_id,code,name) VALUES ('%s','%s','F-1','Invariant farm')"
                    .formatted(farmId, tenantId));
            statement.execute("""
                    INSERT INTO identity.system_manager_profiles
                        (id,user_id,status,availability,qualification_status,created_at,updated_at)
                    VALUES ('%s','%s','ACTIVE','AVAILABLE','QUALIFIED',now(),now())
                    """.formatted(profileId, managerId));
            statement.execute("""
                    INSERT INTO identity.farm_manager_assignments
                        (id,tenant_id,farm_id,system_manager_profile_id,assigned_by,assignment_reason,assigned_at)
                    VALUES ('%s','%s','%s','%s','%s','Initial assignment',now())
                    """.formatted(UUID.randomUUID(), tenantId, farmId, profileId, adminId));

            assertSqlState(statement, """
                    INSERT INTO identity.farm_manager_assignments
                        (id,tenant_id,farm_id,system_manager_profile_id,assigned_by,assignment_reason,assigned_at)
                    VALUES ('%s','%s','%s','%s','%s','Conflicting assignment',now())
                    """.formatted(UUID.randomUUID(), tenantId, farmId, profileId, adminId), "23505");

            statement.execute("""
                    INSERT INTO survey.survey_service_prices
                        (id,survey_service_id,price_per_ha,currency,effective_from,effective_to,created_by)
                    VALUES ('%s','%s',125000.55,'VND','2026-01-01T00:00:00Z','2027-01-01T00:00:00Z','%s')
                    """.formatted(UUID.randomUUID(), serviceId, adminId));
            assertSqlState(statement, """
                    INSERT INTO survey.survey_service_prices
                        (id,survey_service_id,price_per_ha,currency,effective_from,effective_to,created_by)
                    VALUES ('%s','%s',130000.00,'VND','2026-06-01T00:00:00Z','2027-06-01T00:00:00Z','%s')
                    """.formatted(UUID.randomUUID(), serviceId, adminId), "23P01");

            assertThat(queryInt(connection, """
                    SELECT numeric_scale FROM information_schema.columns
                    WHERE table_schema='survey' AND table_name='survey_service_prices' AND column_name='price_per_ha'
                    """)).isEqualTo(2);

            assertThat(statement.executeUpdate("UPDATE farm.farms SET name='Updated', version=version+1 WHERE id='%s' AND version=1"
                    .formatted(farmId))).isEqualTo(1);
            assertThat(statement.executeUpdate("UPDATE farm.farms SET name='Stale' WHERE id='%s' AND version=1"
                    .formatted(farmId))).isZero();
        }
    }

    @Test
    void publicAndCrossServiceRolesCannotWriteOwnedSchemas() throws Exception {
        try (var connection = connection(); var statement = connection.createStatement()) {
            statement.execute("CREATE ROLE phase2_be1_test NOLOGIN");
            statement.execute("CREATE ROLE phase2_be2_test NOLOGIN");
            statement.execute("CREATE SCHEMA be2_probe AUTHORIZATION phase2_be2_test");
            statement.execute("SET ROLE phase2_be2_test");
            statement.execute("CREATE TABLE be2_probe.owned_table(id integer PRIMARY KEY)");
            assertSqlState(statement,
                    "INSERT INTO identity.roles(code,name) VALUES ('ILLEGAL_BE2','Illegal BE2 write')",
                    "42501");
            statement.execute("RESET ROLE");
            statement.execute("SET ROLE phase2_be1_test");
            assertSqlState(statement, "INSERT INTO be2_probe.owned_table(id) VALUES (1)", "42501");
            statement.execute("RESET ROLE");
        }
    }

    @Test
    void nonSuperuserDatabaseOwnerCanApplyAndValidateBaseline() throws Exception {
        String ownerDatabase = "be1_owner_migration_test";
        String ownerRole = "be1_owner_migration_test";
        String ownerPassword = "owner-migration-test-password";

        try (var connection = connection(); var statement = connection.createStatement()) {
            statement.execute("CREATE ROLE " + ownerRole + " LOGIN PASSWORD '" + ownerPassword + "'");
            statement.execute("CREATE DATABASE " + ownerDatabase + " OWNER " + ownerRole);
        }

        String ownerJdbcUrl = "jdbc:postgresql://%s:%d/%s".formatted(
                POSTGRES.getHost(), POSTGRES.getMappedPort(5432), ownerDatabase);
        try (var connection = DriverManager.getConnection(
                        ownerJdbcUrl, POSTGRES.getUsername(), POSTGRES.getPassword());
                var statement = connection.createStatement()) {
            statement.execute("CREATE EXTENSION IF NOT EXISTS pgcrypto");
            statement.execute("CREATE EXTENSION IF NOT EXISTS citext");
            statement.execute("CREATE EXTENSION IF NOT EXISTS btree_gist");
            statement.execute("CREATE EXTENSION IF NOT EXISTS postgis");
        }

        Flyway ownerFlyway = Flyway.configure()
                .dataSource(ownerJdbcUrl, ownerRole, ownerPassword)
                .locations("classpath:db/migration")
                .target("1")
                .validateOnMigrate(true)
                .load();

        assertThat(ownerFlyway.migrate().migrationsExecuted).isEqualTo(1);
        assertThat(ownerFlyway.validateWithResult().validationSuccessful).isTrue();
    }

    private static Connection connection() throws SQLException {
        return DriverManager.getConnection(
                POSTGRES.getJdbcUrl(), POSTGRES.getUsername(), POSTGRES.getPassword());
    }

    private static int queryInt(Connection connection, String sql) throws SQLException {
        try (Statement statement = connection.createStatement(); ResultSet result = statement.executeQuery(sql)) {
            assertThat(result.next()).isTrue();
            return result.getInt(1);
        }
    }

    private static void assertSqlState(Statement statement, String sql, String expectedState) {
        assertThatThrownBy(() -> statement.execute(sql))
                .isInstanceOf(SQLException.class)
                .extracting(error -> ((SQLException) error).getSQLState())
                .isEqualTo(expectedState);
    }
}
