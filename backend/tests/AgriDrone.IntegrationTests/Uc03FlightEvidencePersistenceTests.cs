using System.Text.Json;
using AgriDrone.Database;
using AgriDrone.Modules.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;
using AgriDrone.Modules.Missions.Application.Features.Missions.TransitionMission;
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

public sealed class Uc03FlightEvidencePersistenceTests
{
    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";

    [Fact]
    public async Task ChecklistAndIncidentEvidencePersistWithMission()
    {
        var name = $"agridrone_uc03_{Guid.NewGuid():N}";
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
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            PostgreSqlEnumMappings.ConfigureDataSource(builder);
            await using var dataSource = builder.Build();
            var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
            AgriDroneSchemaDbContextOptions.Configure(options, dataSource);
            await using (var schema = new AgriDroneSchemaDbContext(options.Options))
                await schema.Database.MigrateAsync();

            var actorId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var farmId = Guid.NewGuid();
            var droneId = Guid.NewGuid();
            await using (var seed = new NpgsqlConnection(connectionString))
            {
                await seed.OpenAsync();
                await using var insert = new NpgsqlCommand($"""
                    INSERT INTO identity.users (id, email, password_hash, full_name)
                    VALUES ('{actorId}', 'uc03-{actorId:N}@example.test', 'unused', 'UC03 Actor');
                    INSERT INTO identity.tenants (id, code, name)
                    VALUES ('{tenantId}', 'UC03-{tenantId:N}', 'UC03 Tenant');
                    INSERT INTO farm.farms (id, tenant_id, code, name, created_by)
                    VALUES ('{farmId}', '{tenantId}', 'UC03-FARM', 'UC03 Farm', '{actorId}');
                    INSERT INTO mission.drones
                        (id, code, name, specifications, registration_number,
                         registration_date, registration_expiry_date)
                    VALUES ('{droneId}', 'UC03-{droneId.ToString("N")[..20]}',
                        'UC03 Drone', jsonb_build_object('capabilities',
                            jsonb_build_array('baseline_mapping')),
                        'UC03-REG', '2026-01-01', '2027-01-01');
                    """, seed);
                await insert.ExecuteNonQueryAsync();
            }

            var now = new DateTimeOffset(2026, 10, 20, 3, 0, 0, TimeSpan.Zero);
            using var parameters = JsonDocument.Parse("{}");
            using var items = JsonDocument.Parse("""[{"key":"battery","prompt":"Battery safe?"}]""");
            using var answers = JsonDocument.Parse("""{"battery":true}""");
            var mission = DroneMission.CreateOrderBound(Guid.NewGuid(), tenantId,
                farmId, [], Guid.NewGuid(), droneId, actorId, "UC03-EVIDENCE",
                MissionPurpose.BaselineMapping, Guid.NewGuid(), null, false,
                parameters, actorId, now.AddHours(-2));
            mission.Schedule(now.AddHours(-1), now.AddHours(1), now.AddHours(-2));
            var definition = PreflightChecklistDefinition.CreateActive(
                "DRONE_PRE_FLIGHT", 1, items, actorId, now.AddDays(-1));
            var operationId = Guid.NewGuid();
            mission.CompletePreflight(operationId, "v1", answers, true,
                "Ready", actorId, now.AddMinutes(-5));
            var checklist = MissionPreflightChecklist.Complete(mission.Id,
                definition, operationId, answers, null,
                "Signal loss: land; battery low: return; interruption: abort",
                actorId, now.AddMinutes(-5), now.AddMinutes(-5),
                "Flight permission checked: record UC03-TEST");

            await using (var db = new AgriDroneSchemaDbContext(options.Options))
            {
                db.Set<DroneMission>().Add(mission);
                db.Set<PreflightChecklistDefinition>().Add(definition);
                db.Set<MissionPreflightChecklist>().Add(checklist);
                await db.SaveChangesAsync();
            }

            var managerId = Guid.NewGuid();
            var planning = new Planning(new SurveyOrderMissionPlanningContext(
                mission.SurveyOrderId!.Value, tenantId, farmId,
                SurveyServiceType.PlantHealth, true, null, [],
                now.AddHours(-1), now.AddHours(1), true, null,
                true, true, mission.FarmBoundaryVersionId, managerId));
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AgriDrone"] = connectionString
                }).Build();
            var services = new ServiceCollection();
            services.AddMissionsModule(configuration);
            await using (var provider = services.BuildServiceProvider())
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MissionsDbContext>();
                var handler = new TransitionMissionCommandHandler(
                    new DroneMissionRepository(db), new DroneRepository(db),
                    new DroneMaintenanceRepository(db), new PreflightChecklistRepository(db),
                    new MissionFieldNoteRepository(db),
                    new Access(tenantId, farmId, managerId), planning, db,
                    new Audit(), new Actor(actorId), new FixedClock(now));
                var storedMission = await db.DroneMissions.SingleAsync(value =>
                    value.Id == mission.Id);
                var started = await handler.Handle(new TransitionMissionCommand(
                    farmId, mission.Id, MissionStatus.InFlight,
                    storedMission.Version, null), CancellationToken.None);
                Assert.True(started.IsSuccess);
            }
            var note = MissionFieldNote.Create(tenantId, farmId, mission.Id,
                Guid.NewGuid(), actorId, "Signal recovered", now.AddMinutes(1),
                now.AddMinutes(2), "SIGNAL_LOSS", "Returned to safe route",
                "CONTINUE", "media:flight-log-uc03");
            await using (var db = new AgriDroneSchemaDbContext(options.Options))
            {
                db.Set<MissionFieldNote>().Add(note);
                await db.SaveChangesAsync();
            }

            await using (var db = new AgriDroneSchemaDbContext(options.Options))
            {
                var storedChecklist = await db.Set<MissionPreflightChecklist>()
                    .AsNoTracking().SingleAsync(value => value.MissionId == mission.Id);
                var storedNote = await db.Set<MissionFieldNote>()
                    .AsNoTracking().SingleAsync(value => value.MissionId == mission.Id);
                Assert.Equal("Flight permission checked: record UC03-TEST",
                    storedChecklist.FlightAuthorizationEvidence);
                Assert.Equal("SIGNAL_LOSS", storedNote.IncidentType);
                Assert.Equal("CONTINUE", storedNote.RecoveryDecision);
                Assert.Equal("media:flight-log-uc03", storedNote.EvidenceReference);
                Assert.Equal(actorId, storedNote.CreatedBy);
                Assert.Equal(MissionStatus.InFlight,
                    (await db.Set<DroneMission>().AsNoTracking()
                        .SingleAsync(value => value.Id == mission.Id)).Status);
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

    private sealed class Planning(SurveyOrderMissionPlanningContext context)
        : ISurveyOrderMissionPlanningQuery
    {
        public Task<SurveyOrderMissionPlanningContext?> GetAsync(Guid orderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyOrderMissionPlanningContext?>(context);
    }

    private sealed class Access(Guid tenantId, Guid farmId, Guid managerId)
        : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(Guid requestedFarmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SystemManagerFarmAccess.Allowed(tenantId, farmId, managerId));
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

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Audit : IAuditWriter
    {
        public void AddUserAction(IAuditLogSink sink, Guid tenantId, Guid? farmId,
            Guid actorId, Guid correlationId, string entityType, Guid entityId,
            string action, JsonDocument? oldData, JsonDocument? newData,
            DateTimeOffset createdAt) => sink.AddAuditLog(AuditLog.ForUserAction(
                tenantId, farmId, actorId, correlationId, entityType, entityId,
                action, oldData, newData, createdAt));

        public void AddSystemAdminAction(IAuditLogSink sink, Guid actorId,
            Guid correlationId, string entityType, Guid entityId, string action,
            JsonDocument? oldData, JsonDocument? newData, DateTimeOffset createdAt) =>
            throw new NotSupportedException();
    }
}
