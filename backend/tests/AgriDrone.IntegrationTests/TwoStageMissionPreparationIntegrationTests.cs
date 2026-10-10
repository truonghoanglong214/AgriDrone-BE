using System.Text.Json;
using AgriDrone.Database;
using AgriDrone.Modules.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;
using AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.Modules.Missions.Infrastructure.Repositories;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class TwoStageMissionPreparationIntegrationTests
{
    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";

    [Fact]
    public async Task BaselineThenPublishedMapAndPaymentPersistsServiceMission()
    {
        var name = $"agridrone_uc02_{Guid.NewGuid():N}";
        var connectionString =
            $"Host=127.0.0.1;Port=55432;Database={name};Username=agridrone_test;Password=agridrone_test";
        await using (var admin = new NpgsqlConnection(AdminConnection))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var sourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            PostgreSqlEnumMappings.ConfigureDataSource(sourceBuilder);
            await using (var source = sourceBuilder.Build())
            {
                var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
                AgriDroneSchemaDbContextOptions.Configure(options, source);
                await using var schema = new AgriDroneSchemaDbContext(options.Options);
                await schema.Database.MigrateAsync();
            }

            var actorId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var farmId = Guid.NewGuid();
            var droneId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var boundaryId = Guid.NewGuid();
            var zoneIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var start = new DateTimeOffset(2026, 10, 20, 1, 0, 0, TimeSpan.Zero);
            await using (var seed = new NpgsqlConnection(connectionString))
            {
                await seed.OpenAsync();
                await using var insert = new NpgsqlCommand($"""
                    INSERT INTO identity.users (id, email, password_hash, full_name)
                    VALUES ('{actorId}', 'uc02-{actorId:N}@example.test', 'unused', 'UC02 Actor');
                    INSERT INTO identity.tenants (id, code, name)
                    VALUES ('{tenantId}', 'UC02-{tenantId:N}', 'UC02 Tenant');
                    INSERT INTO farm.farms (id, tenant_id, code, name, created_by)
                    VALUES ('{farmId}', '{tenantId}', 'UC02-FARM', 'UC02 Farm', '{actorId}');
                    INSERT INTO mission.drones (id, code, name)
                    VALUES ('{droneId}', 'UC02-{droneId.ToString("N")[..20]}', 'UC02 Drone');
                    """, seed);
                await insert.ExecuteNonQueryAsync();
            }

            var planning = new StagedPlanning(new SurveyOrderMissionPlanningContext(
                orderId, tenantId, farmId, SurveyServiceType.PlantHealth,
                true, null, zoneIds, start, start.AddHours(2), true,
                null, true, true, boundaryId));
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AgriDrone"] = connectionString
                }).Build();
            var services = new ServiceCollection();
            services.AddMissionsModule(configuration);
            await using var provider = services.BuildServiceProvider();

            async Task<AgriDrone.SharedKernel.Application.Result<PrepareMissionSetResult>> Prepare(
                MissionScheduleWindow? service, MissionScheduleWindow? baseline)
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<MissionsDbContext>();
                var handler = new PrepareMissionSetCommandHandler(
                    planning, new Access(tenantId, farmId),
                    new DroneMissionRepository(db), new AvailableDrone(droneId),
                    db, new Audit(), new Actor(actorId), TimeProvider.System);
                return await handler.Handle(new PrepareMissionSetCommand(
                    orderId, droneId, Guid.NewGuid(), service, baseline),
                    CancellationToken.None);
            }

            var baseline = await Prepare(null,
                new MissionScheduleWindow(start.AddMinutes(10), start.AddMinutes(50)));
            Assert.True(baseline.IsSuccess);
            Assert.Single(baseline.Value.Missions);
            await AssertPersisted(1);

            planning.Context = planning.Context with
            {
                RequiresBaselineMapping = false,
                CurrentBaseMapVersionId = Guid.NewGuid(),
                IsEligibleForPlanning = false,
                IsReadyToSchedule = false,
                ReadinessFailureCode = "PaymentNotConfirmed"
            };
            var unpaid = await Prepare(new MissionScheduleWindow(
                start.AddHours(1), start.AddHours(2)), null);
            Assert.True(unpaid.IsFailure);
            Assert.Equal("MissionPlanning.OrderNotReady", unpaid.Error.Code);
            await AssertPersisted(1);

            planning.Context = planning.Context with
            {
                IsEligibleForPlanning = true,
                IsReadyToSchedule = true,
                IsReadyForOperations = true,
                ReadinessFailureCode = null
            };
            var paid = await Prepare(new MissionScheduleWindow(
                start.AddHours(1), start.AddHours(2)), null);
            Assert.True(paid.IsSuccess);
            Assert.Equal(2, paid.Value.Missions.Count);
            await AssertPersisted(2);

            async Task AssertPersisted(int expected)
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<MissionsDbContext>();
                var missions = await db.DroneMissions.AsNoTracking()
                    .Where(value => value.SurveyOrderId == orderId)
                    .OrderBy(value => value.Purpose).ToListAsync();
                Assert.Equal(expected, missions.Count);
                Assert.All(missions, value =>
                {
                    Assert.Equal(boundaryId, value.FarmBoundaryVersionId);
                    Assert.Equal(zoneIds, value.ScopeZoneIds);
                });
                if (expected == 2)
                    Assert.Equal(planning.Context.CurrentBaseMapVersionId,
                        missions.Single(value => value.Purpose == MissionPurpose.PlantHealth)
                            .SourceMapVersionId);
            }
        }
        finally
        {
            await using var admin = new NpgsqlConnection(AdminConnection);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS {name} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class StagedPlanning(SurveyOrderMissionPlanningContext context)
        : ISurveyOrderMissionPlanningQuery
    {
        public SurveyOrderMissionPlanningContext Context { get; set; } = context;

        public Task<SurveyOrderMissionPlanningContext?> GetAsync(Guid surveyOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyOrderMissionPlanningContext?>(Context);
    }

    private sealed class Access(Guid tenantId, Guid farmId) : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(Guid requestedFarmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SystemManagerFarmAccess.Allowed(tenantId, farmId,
                Guid.NewGuid()));
    }

    private sealed class AvailableDrone(Guid droneId) : IDroneQueries
    {
        public Task<IReadOnlyList<AvailableDroneResponse>> GetAvailableAsync(
            DateTimeOffset startAt, DateTimeOffset endAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AvailableDroneResponse>>(
                [new AvailableDroneResponse(droneId, "UC02", "Drone", null, null,
                    JsonSerializer.Deserialize<JsonElement>(
                        """{"capabilities":["baseline_mapping","plant_health"]}"""),
                    null, null, null, DroneStatus.Available, null)]);

        public Task<IReadOnlyList<DroneRegistryItemResponse>> GetRegistryAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DroneRegistryItemResponse>>([]);
    }

    private sealed class Actor(Guid actorId) : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => null;
        public Guid? ActorId => actorId;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class Audit : IAuditWriter
    {
        public void AddUserAction(IAuditLogSink sink, Guid tenantId, Guid? farmId,
            Guid actorId, Guid correlationId, string entityType, Guid entityId,
            string action, JsonDocument? oldData, JsonDocument? newData,
            DateTimeOffset createdAt) => sink.AddAuditLog(AuditLog.ForUserAction(
                tenantId, farmId, actorId, correlationId, entityType,
                entityId, action, oldData, newData, createdAt));

        public void AddSystemAdminAction(IAuditLogSink sink, Guid actorId,
            Guid correlationId, string entityType, Guid entityId, string action,
            JsonDocument? oldData, JsonDocument? newData, DateTimeOffset createdAt) =>
            throw new NotSupportedException();
    }
}
