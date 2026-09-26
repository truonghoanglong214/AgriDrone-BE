using AgriDrone.Database;
using AgriDrone.Modules.Plants;
using AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition;
using AgriDrone.Modules.Plants.Application.Features.GetActivePlantConditions;
using AgriDrone.Modules.Plants.Application.Features.RetirePlantCondition;
using AgriDrone.Modules.Plants.Application.Features.VersionPlantCondition;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Execution;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Text.Json;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class CoreMasterDataLifecycleIntegrationTests
{
    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;" +
        "Username=agridrone_test;Password=agridrone_test";

    private static readonly Guid TestActorId =
        Guid.Parse("7906907d-6911-4f05-8dd5-95f63a1cc40d");

    [Fact]
    public async Task RetiredDefinitionsAreUnavailableToNewRecordCatalogs()
    {
        const string databaseName = "agridrone_uc04_active_catalog";
        var connectionString = GetConnectionString(databaseName);
        await RecreateAndMigrateDatabaseAsync(databaseName, connectionString);
        await using var provider = CreateServiceProvider(connectionString);

        var condition = await SendAsync(
            provider,
            new CreatePlantConditionCommand(
                "UC04_RETIRED_CONDITION",
                "UC04 retired condition",
                null,
                ConditionType.Disease,
                "Integration-test condition."));
        Assert.True(
            condition.IsSuccess,
            $"{condition.Error.Code}: {condition.Error.Description}");

        var retireCondition = await SendAsync(
            provider,
            new RetirePlantConditionCommand(
                condition.Value.Id,
                condition.Value.Version));
        Assert.True(
            retireCondition.IsSuccess,
            $"{retireCondition.Error.Code}: {retireCondition.Error.Description}");

        var activeConditions = await SendAsync(
            provider,
            new GetActivePlantConditionsQuery());

        Assert.True(activeConditions.IsSuccess);
        Assert.DoesNotContain(
            activeConditions.Value,
            item => item.Id == condition.Value.Id);

        await AssertDatabaseWriteRejectedAsync(
            connectionString,
            """
            INSERT INTO plant.condition_detections
                (plant_scan_id, condition_id, severity_level_id)
            SELECT gen_random_uuid(), @definition_id, id
            FROM plant.health_levels
            WHERE code = 'MILD';
            """,
            condition.Value.Id,
            "ck_condition_detection_active_condition");

        var activeDisease = await SendAsync(
            provider,
            new CreatePlantConditionCommand(
                "UC04_ACTIVE_DISEASE",
                "UC04 active disease",
                null,
                ConditionType.Disease,
                null));
        Assert.True(activeDisease.IsSuccess);

        await AssertDatabaseWriteRejectedAsync(
            connectionString,
            """
            INSERT INTO plant.condition_detections
                (plant_scan_id, condition_id, severity_level_id)
            SELECT gen_random_uuid(), @definition_id, id
            FROM plant.health_levels
            WHERE code = 'HEALTHY';
            """,
            activeDisease.Value.Id,
            "ck_condition_detection_disease_severity");
    }

    [Fact]
    public async Task VersioningPreservesOldLabelsAndRevisionIdentity()
    {
        const string databaseName = "agridrone_uc04_history";
        var connectionString = GetConnectionString(databaseName);
        await RecreateAndMigrateDatabaseAsync(databaseName, connectionString);
        await using var provider = CreateServiceProvider(connectionString);

        var condition = await SendAsync(
            provider,
            new CreatePlantConditionCommand(
                "UC04_HISTORY_CONDITION",
                "Original condition label",
                "Original scientific name",
                ConditionType.Disease,
                "Original description."));
        Assert.True(condition.IsSuccess);

        var nextCondition = await SendAsync(
            provider,
            new VersionPlantConditionCommand(
                condition.Value.Id,
                "Updated condition label",
                "Updated scientific name",
                "Updated description.",
                condition.Value.Version));
        Assert.True(
            nextCondition.IsSuccess,
            $"{nextCondition.Error.Code}: {nextCondition.Error.Description}");

        var conditionHistory = await ReadHistoryAsync(
            connectionString,
            "plant.plant_conditions",
            "UC04_HISTORY_CONDITION");
        Assert.Collection(
            conditionHistory,
            original =>
            {
                Assert.Equal(condition.Value.Id, original.Id);
                Assert.Equal("Original condition label", original.Name);
                Assert.Equal(1, original.RevisionNumber);
                Assert.Null(original.SupersedesId);
                Assert.False(original.IsActive);
                Assert.True(original.HasRetiredAt);
            },
            current =>
            {
                Assert.Equal(nextCondition.Value.Id, current.Id);
                Assert.Equal("Updated condition label", current.Name);
                Assert.Equal(2, current.RevisionNumber);
                Assert.Equal(condition.Value.Id, current.SupersedesId);
                Assert.True(current.IsActive);
                Assert.False(current.HasRetiredAt);
            });

        var auditActions = await ReadAuditActionsAsync(connectionString);
        Assert.Equal(
            ["CREATE", "VERSION"],
            auditActions);
    }

    [Fact]
    public async Task MigrationSeedsRequiredHealthLevelsAndMissingUnknownFailsValidation()
    {
        const string databaseName = "agridrone_uc04_health_seeds";
        var connectionString = GetConnectionString(databaseName);
        await RecreateAndMigrateDatabaseAsync(databaseName, connectionString);

        var actual = await ReadHealthLevelsAsync(connectionString);
        Assert.Collection(
            actual,
            level => AssertHealthLevel(
                level, "UNKNOWN", null, isHealthy: false),
            level => AssertHealthLevel(
                level, "HEALTHY", 0, isHealthy: true),
            level => AssertHealthLevel(
                level, "MILD", 1, isHealthy: false),
            level => AssertHealthLevel(
                level, "MODERATE", 2, isHealthy: false),
            level => AssertHealthLevel(
                level, "SEVERE", 3, isHealthy: false));

        await using var provider = CreateServiceProvider(connectionString);
        await provider.ValidateCoreMasterDataAsync();

        var immutableException = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(
                connectionString,
                "DELETE FROM plant.health_levels WHERE code = 'UNKNOWN';"));
        Assert.Equal("23514", immutableException.SqlState);
        Assert.Equal(
            "ck_health_levels_required_immutable",
            immutableException.ConstraintName);

        await ExecuteAsync(
            connectionString,
            """
            SET session_replication_role = replica;
            DELETE FROM plant.health_levels WHERE code = 'UNKNOWN';
            SET session_replication_role = DEFAULT;
            """);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.ValidateCoreMasterDataAsync());
        Assert.Contains(
            "Required health level 'UNKNOWN' is missing.",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static async Task<TResponse> SendAsync<TResponse>(
        IServiceProvider provider,
        IRequest<TResponse> request)
    {
        await using var scope = provider.CreateAsyncScope();
        var initializer = scope.ServiceProvider
            .GetRequiredService<IExecutionContextInitializer>();
        using var contextLease = initializer.Begin(
            ExecutionContextSnapshot.ForHttp(
                tenantId: null,
                actorId: TestActorId,
                correlationId: Guid.NewGuid()));
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request);
    }

    private static ServiceProvider CreateServiceProvider(
        string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] = connectionString
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAuditWriter, TestAuditWriter>();
        services.AddExecutionContext();
        services.AddPlantsModule(configuration);
        return services.BuildServiceProvider();
    }

    private static async Task RecreateAndMigrateDatabaseAsync(
        string databaseName,
        string connectionString)
    {
        await using (var connection = new NpgsqlConnection(AdminConnection))
        {
            await connection.OpenAsync();
            await using var drop = connection.CreateCommand();
            drop.CommandText =
                $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
            await using var create = connection.CreateCommand();
            create.CommandText = $"CREATE DATABASE {databaseName}";
            await create.ExecuteNonQueryAsync();
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
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

        await ExecuteAsync(
            connectionString,
            $"""
            INSERT INTO identity.users (id, email, password_hash, full_name)
            VALUES ('{TestActorId}', 'uc04-admin@example.test', 'not-used', 'UC04 Test Admin');
            """);
    }

    private static async Task<IReadOnlyList<DefinitionRevision>>
        ReadHistoryAsync(
            string connectionString,
            string tableName,
            string code)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT id, name, revision_number, supersedes_id, is_active,
                   retired_at IS NOT NULL
            FROM {tableName}
            WHERE code = @code
            ORDER BY revision_number;
            """;
        command.Parameters.AddWithValue("code", code);

        var result = new List<DefinitionRevision>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new DefinitionRevision(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.GetBoolean(4),
                reader.GetBoolean(5)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<HealthLevelRow>>
        ReadHealthLevelsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT code, rank, is_healthy, is_active
            FROM plant.health_levels
            WHERE code IN ('UNKNOWN', 'HEALTHY', 'MILD', 'MODERATE', 'SEVERE')
            ORDER BY CASE code
                WHEN 'UNKNOWN' THEN 0
                WHEN 'HEALTHY' THEN 1
                WHEN 'MILD' THEN 2
                WHEN 'MODERATE' THEN 3
                WHEN 'SEVERE' THEN 4
                ELSE 5
            END;
            """;

        var result = new List<HealthLevelRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new HealthLevelRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetBoolean(2),
                reader.GetBoolean(3)));
        }

        return result;
    }

    private static async Task ExecuteAsync(
        string connectionString,
        string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertDatabaseWriteRejectedAsync(
        string connectionString,
        string sql,
        Guid definitionId,
        string expectedConstraint)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("definition_id", definitionId);
            await command.ExecuteNonQueryAsync();
        });

        Assert.Equal("23514", exception.SqlState);
        Assert.Equal(expectedConstraint, exception.ConstraintName);
    }

    private static async Task<IReadOnlyList<string>> ReadAuditActionsAsync(
        string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT action
            FROM system.audit_logs
            WHERE entity_type = 'PlantCondition'
            ORDER BY id;
            """;

        var actions = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            actions.Add(reader.GetString(0));
        }

        return actions;
    }

    private static string GetConnectionString(string databaseName) =>
        "Host=127.0.0.1;Port=55432;Database=" + databaseName +
        ";Username=agridrone_test;Password=agridrone_test;Pooling=false";

    private static void AssertHealthLevel(
        HealthLevelRow actual,
        string code,
        int? rank,
        bool isHealthy)
    {
        Assert.Equal(code, actual.Code);
        Assert.Equal(rank, actual.Rank);
        Assert.Equal(isHealthy, actual.IsHealthy);
        Assert.True(actual.IsActive);
    }

    private sealed record DefinitionRevision(
        Guid Id,
        string Name,
        int RevisionNumber,
        Guid? SupersedesId,
        bool IsActive,
        bool HasRetiredAt);

    private sealed record HealthLevelRow(
        string Code,
        int? Rank,
        bool IsHealthy,
        bool IsActive);

    private sealed class TestAuditWriter : IAuditWriter
    {
        public void AddUserAction(
            IAuditLogSink sink,
            Guid tenantId,
            Guid? farmId,
            Guid actorId,
            Guid correlationId,
            string entityType,
            Guid entityId,
            string action,
            JsonDocument? oldData,
            JsonDocument? newData,
            DateTimeOffset createdAt) =>
            throw new NotSupportedException();

        public void AddSystemAdminAction(
            IAuditLogSink sink,
            Guid actorId,
            Guid correlationId,
            string entityType,
            Guid entityId,
            string action,
            JsonDocument? oldData,
            JsonDocument? newData,
            DateTimeOffset createdAt) =>
            sink.AddAuditLog(AuditLog.ForSystemAdminAction(
                actorId,
                correlationId,
                entityType,
                entityId,
                action,
                oldData,
                newData,
                createdAt));
    }
}
