using AgriDrone.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class OrderBoundDroneReservationIntegrationTests
{
    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";

    [Fact]
    public async Task ConcurrentSchedulesAcrossTenantsReserveDroneOnlyOnce()
    {
        var databaseName = $"agridrone_uc02_{Guid.NewGuid():N}";
        var connectionString =
            $"Host=127.0.0.1;Port=55432;Database={databaseName};Username=agridrone_test;Password=agridrone_test";
        try
        {
            await using (var admin = new NpgsqlConnection(AdminConnection))
            {
                await admin.OpenAsync();
                await using var create = new NpgsqlCommand(
                    $"CREATE DATABASE {databaseName}", admin);
                await create.ExecuteNonQueryAsync();
            }

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            PostgreSqlEnumMappings.ConfigureDataSource(dataSourceBuilder);
            await using (var dataSource = dataSourceBuilder.Build())
            {
                var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
                AgriDroneSchemaDbContextOptions.Configure(options, dataSource);
                await using var context = new AgriDroneSchemaDbContext(options.Options);
                await context.Database.MigrateAsync();
            }

            var actorId = Guid.NewGuid();
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();
            var farmA = Guid.NewGuid();
            var farmB = Guid.NewGuid();
            var droneId = Guid.NewGuid();
            await using (var seed = new NpgsqlConnection(connectionString))
            {
                await seed.OpenAsync();
                await using var command = new NpgsqlCommand(
                    $"""
                    INSERT INTO identity.users (id, email, password_hash, full_name)
                    VALUES ('{actorId}', 'uc02-{actorId:N}@example.test', 'unused', 'UC02 Actor');
                    INSERT INTO identity.tenants (id, code, name)
                    VALUES ('{tenantA}', 'UC02-{tenantA:N}', 'Tenant A'),
                           ('{tenantB}', 'UC02-{tenantB:N}', 'Tenant B');
                    INSERT INTO farm.farms (id, tenant_id, code, name, created_by)
                    VALUES ('{farmA}', '{tenantA}', 'UC02-A', 'Farm A', '{actorId}'),
                           ('{farmB}', '{tenantB}', 'UC02-B', 'Farm B', '{actorId}');
                    INSERT INTO mission.drones (id, code, name)
                    VALUES ('{droneId}', 'UC02-{droneId.ToString("N")[..20]}', 'Shared drone');
                    """, seed);
                await command.ExecuteNonQueryAsync();
            }

            await using var first = new NpgsqlConnection(connectionString);
            await using var second = new NpgsqlConnection(connectionString);
            await first.OpenAsync();
            await second.OpenAsync();
            await using var transaction = await first.BeginTransactionAsync();
            await using (var firstInsert = new NpgsqlCommand(
                MissionInsert(Guid.NewGuid(), tenantA, farmA, droneId, actorId),
                first, transaction))
                await firstInsert.ExecuteNonQueryAsync();

            await using var secondInsert = new NpgsqlCommand(
                MissionInsert(Guid.NewGuid(), tenantB, farmB, droneId, actorId),
                second);
            var competingInsert = secondInsert.ExecuteNonQueryAsync();
            await Task.Delay(100);
            Assert.False(competingInsert.IsCompleted);

            await transaction.CommitAsync();
            var conflict = await Assert.ThrowsAsync<PostgresException>(
                async () => await competingInsert);
            Assert.Equal(PostgresErrorCodes.ExclusionViolation, conflict.SqlState);
            Assert.Equal("ex_drone_missions_no_schedule_overlap",
                conflict.ConstraintName);

            await using var count = new NpgsqlCommand(
                $"SELECT count(*) FROM mission.drone_missions WHERE drone_id = '{droneId}'",
                first);
            Assert.Equal(1L, await count.ExecuteScalarAsync());
        }
        finally
        {
            await using var admin = new NpgsqlConnection(AdminConnection);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static string MissionInsert(
        Guid missionId, Guid tenantId, Guid farmId, Guid droneId, Guid actorId) =>
        $"""
        INSERT INTO mission.drone_missions
            (id, tenant_id, farm_id, drone_id, mission_code, mission_type,
             status, scheduled_at, scheduled_end_at, created_by)
        VALUES
            ('{missionId}', '{tenantId}', '{farmId}', '{droneId}',
             'UC02-{missionId.ToString("N")[..20]}',
             'MAPPING'::system.mission_type, 'SCHEDULED'::system.mission_status,
             '2026-10-20T01:00:00Z', '2026-10-20T02:00:00Z', '{actorId}');
        """;
}
