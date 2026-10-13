using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;
using AgriDrone.Modules.Missions.Application.Features.Missions.RescheduleOrderMission;
using AgriDrone.Modules.Missions.Application.Features.Missions.TransitionMission;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class StartFlightGateTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 20, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BaselineMayStartBeforePaymentWhenSafetyAndAppointmentAreReady()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        var result = await fixture.Start();

        Assert.True(result.IsSuccess);
        Assert.Equal(MissionStatus.InFlight, fixture.Mission.Status);
        Assert.Equal(1, fixture.UnitOfWork.SaveCount);
        Assert.Contains("START_FLIGHT", fixture.Audit.Actions);
    }

    [Fact]
    public async Task PaidMissionCannotStartWhenPaymentIsRevokedAfterPreparation()
    {
        var fixture = new Fixture(MissionPurpose.PlantHealth);
        fixture.Planning.Context = fixture.Planning.Context with
        {
            IsReadyForOperations = false,
            ReadinessFailureCode = "PaymentNotConfirmed"
        };

        var result = await fixture.Start();

        Assert.True(result.IsFailure);
        Assert.Equal("MissionOperation.OrderNotReady", result.Error.Code);
        Assert.Equal(MissionStatus.Scheduled, fixture.Mission.Status);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task RevokedAppointmentCannotStartFlight()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        fixture.Planning.Context = fixture.Planning.Context with
        {
            IsReadyToSchedule = false,
            ReadinessFailureCode = "AppointmentNotConfirmed"
        };

        var result = await fixture.Start();

        Assert.Equal("MissionOperation.OrderNotReady", result.Error.Code);
        Assert.Equal(MissionStatus.Scheduled, fixture.Mission.Status);
    }

    [Fact]
    public async Task ChangedBoundaryOrZonePlanCannotStartFlight()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        fixture.Planning.Context = fixture.Planning.Context with
        {
            FarmBoundaryVersionId = Guid.NewGuid()
        };
        var boundary = await fixture.Start();
        Assert.Equal("MissionOperation.InvalidOrderContext", boundary.Error.Code);

        fixture.Planning.Context = fixture.Planning.Context with
        {
            FarmBoundaryVersionId = fixture.Mission.FarmBoundaryVersionId,
            ScopeZoneIds = [Guid.NewGuid()]
        };
        var scope = await fixture.Start();
        Assert.Equal("MissionOperation.InvalidOrderContext", scope.Error.Code);
        Assert.Equal(MissionStatus.Scheduled, fixture.Mission.Status);
    }

    [Fact]
    public async Task RevokedManagerAssignmentCannotStartFlight()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        fixture.Planning.Context = fixture.Planning.Context with
        {
            PrimarySystemManagerId = Guid.NewGuid()
        };

        var result = await fixture.Start();

        Assert.Equal("MissionOperation.InvalidOrderContext", result.Error.Code);
        Assert.Equal(MissionStatus.Scheduled, fixture.Mission.Status);
    }

    [Fact]
    public async Task ChecklistFromPreviousManagerCannotStartFlight()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping,
            preflightByDifferentActor: true);

        var result = await fixture.Start();

        Assert.Equal("MissionOperation.PreflightRequired", result.Error.Code);
        Assert.Equal(MissionStatus.Scheduled, fixture.Mission.Status);
    }

    [Fact]
    public async Task MissingFlightAuthorizationEvidenceCannotStartFlight()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping,
            includeSafetyEvidence: false);

        var result = await fixture.Start();

        Assert.Equal("MissionOperation.SafetyEvidenceRequired", result.Error.Code);
        Assert.Equal(MissionStatus.Scheduled, fixture.Mission.Status);
    }

    [Fact]
    public async Task UnsuitableFlightDecisionCannotStartFlight()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping,
            suitableForFlight: false);

        var result = await fixture.Start();

        Assert.Equal("MissionOperation.PreflightRequired", result.Error.Code);
        Assert.Equal(MissionStatus.Scheduled, fixture.Mission.Status);
    }

    [Fact]
    public async Task FlightFailureKeepsAuditAndOpensMaintenanceRecovery()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        Assert.True((await fixture.Start()).IsSuccess);

        var result = await fixture.Handler.Handle(new TransitionMissionCommand(
            fixture.FarmId, fixture.Mission.Id, MissionStatus.FlightFailed,
            fixture.Mission.Version, "LOW_BATTERY; safe landing",
            Guid.NewGuid(), "LOW_BATTERY", "Landed safely",
            "RESCHEDULE_REQUIRED", "media:flight-log"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MissionStatus.FlightFailed, fixture.Mission.Status);
        Assert.Single(fixture.Maintenance.Records);
        Assert.Equal("RESCHEDULE_REQUIRED",
            Assert.Single(fixture.Notes.Items).RecoveryDecision);
        Assert.Contains("FAIL_FLIGHT", fixture.Audit.Actions);
    }

    [Fact]
    public async Task FlightFailureWithoutIncidentCannotChangeState()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        Assert.True((await fixture.Start()).IsSuccess);

        var result = await fixture.Handler.Handle(new TransitionMissionCommand(
            fixture.FarmId, fixture.Mission.Id, MissionStatus.FlightFailed,
            fixture.Mission.Version, "Signal lost"), CancellationToken.None);

        Assert.Equal("MissionOperation.FailureIncidentRequired", result.Error.Code);
        Assert.Equal(MissionStatus.InFlight, fixture.Mission.Status);
        Assert.Empty(fixture.Notes.Items);
        Assert.Empty(fixture.Maintenance.Records);
    }

    [Fact]
    public async Task RecoveryRequiresRecordedDecisionAndCompletedDroneMaintenance()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        Assert.True((await fixture.Start()).IsSuccess);
        var failed = await fixture.FailFlight("RESCHEDULE_REQUIRED");
        Assert.True(failed.IsSuccess);
        Assert.Equal(DroneStatus.Maintenance, fixture.Drone.Status);

        var beforeMaintenance = await fixture.Recover();
        Assert.Equal("Mission.DroneNotAvailable", beforeMaintenance.Error.Code);
        fixture.Drone.CompleteMaintenance(Now, null);

        var recovered = await fixture.Recover();
        Assert.True(recovered.IsSuccess);
        Assert.Equal(MissionStatus.Scheduled, fixture.Mission.Status);
        Assert.Null(fixture.Mission.StartedAt);
        Assert.Null(fixture.Mission.EndedAt);
        Assert.Null(fixture.Mission.PreflightOperationId);
        Assert.Contains("RECOVER_FAILED_FLIGHT", fixture.Audit.Actions);
        Assert.Single(fixture.Notes.Items);
    }

    [Fact]
    public async Task AbortDecisionCannotBeRescheduled()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        Assert.True((await fixture.Start()).IsSuccess);
        Assert.True((await fixture.FailFlight("ABORT")).IsSuccess);
        fixture.Drone.CompleteMaintenance(Now, null);

        var result = await fixture.Recover();

        Assert.Equal("MissionOperation.RecoveryDecisionRequired", result.Error.Code);
        Assert.Equal(MissionStatus.FlightFailed, fixture.Mission.Status);
    }

    [Fact]
    public async Task FailedFlightMayRecoverWithAvailableReplacementDrone()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        Assert.True((await fixture.Start()).IsSuccess);
        Assert.True((await fixture.FailFlight("RESCHEDULE_REQUIRED")).IsSuccess);

        var recovered = await fixture.Recover(fixture.ReplacementDrone.Id);

        Assert.True(recovered.IsSuccess);
        Assert.Equal(fixture.ReplacementDrone.Id, fixture.Mission.DroneId);
        Assert.Equal(DroneStatus.Maintenance, fixture.Drone.Status);
        Assert.Equal(MissionStatus.Scheduled, fixture.Mission.Status);
    }

    [Fact]
    public async Task RecoveryRejectsStaleMissionVersion()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        Assert.True((await fixture.Start()).IsSuccess);
        Assert.True((await fixture.FailFlight("RESCHEDULE_REQUIRED")).IsSuccess);
        fixture.Drone.CompleteMaintenance(Now, null);

        var result = await fixture.Recover(expectedVersion: fixture.Mission.Version + 1);

        Assert.Equal("Mission.VersionConflict", result.Error.Code);
        Assert.Equal(MissionStatus.FlightFailed, fixture.Mission.Status);
    }

    [Fact]
    public async Task StaleMissionVersionCannotStartFlight()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        var result = await fixture.Handler.Handle(new TransitionMissionCommand(
            fixture.FarmId, fixture.Mission.Id, MissionStatus.InFlight,
            fixture.Mission.Version + 1, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MissionStatus.Scheduled, fixture.Mission.Status);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CompletedFlightCannotBeStartedAgain()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        Assert.True((await fixture.Start()).IsSuccess);
        var completed = await fixture.Handler.Handle(new TransitionMissionCommand(
            fixture.FarmId, fixture.Mission.Id, MissionStatus.FlightCompleted,
            fixture.Mission.Version, null), CancellationToken.None);
        Assert.True(completed.IsSuccess);

        var replay = await fixture.Start();

        Assert.True(replay.IsFailure);
        Assert.Equal(MissionStatus.FlightCompleted, fixture.Mission.Status);
    }

    [Fact]
    public async Task FlightFailureWithoutReasonCannotBypassHandlerValidation()
    {
        var fixture = new Fixture(MissionPurpose.BaselineMapping);
        Assert.True((await fixture.Start()).IsSuccess);

        var result = await fixture.Handler.Handle(new TransitionMissionCommand(
            fixture.FarmId, fixture.Mission.Id, MissionStatus.FlightFailed,
            fixture.Mission.Version, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionOperation.ReasonRequired", result.Error.Code);
        Assert.Equal(MissionStatus.InFlight, fixture.Mission.Status);
        Assert.Empty(fixture.Maintenance.Records);
    }

    private sealed class Fixture
    {
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly Guid _actorId = Guid.NewGuid();
        private readonly Guid _managerId = Guid.NewGuid();

        public Fixture(MissionPurpose purpose, bool includeSafetyEvidence = true,
            bool preflightByDifferentActor = false,
            bool suitableForFlight = true)
        {
            FarmId = Guid.NewGuid();
            using var specifications = JsonDocument.Parse(
                """{"capabilities":["baseline_mapping","plant_health"]}""");
            Drone = Drone.Create("GATE-DRONE", "Gate Drone", null, null,
                specifications.RootElement, null, "REG-UC03",
                new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1),
                null, null, Now.AddDays(-1));
            ReplacementDrone = Drone.Create("GATE-BACKUP", "Backup Drone", null, null,
                specifications.RootElement, null, "REG-UC03-BACKUP",
                new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1),
                null, null, Now.AddDays(-1));
            var orderId = Guid.NewGuid();
            var boundaryId = Guid.NewGuid();
            var zoneIds = new[] { Guid.NewGuid() };
            var mapId = purpose == MissionPurpose.BaselineMapping
                ? (Guid?)null : Guid.NewGuid();
            using var parameters = JsonDocument.Parse("{}");
            Mission = DroneMission.CreateOrderBound(orderId, _tenantId,
                FarmId, zoneIds, boundaryId, Drone.Id, _actorId,
                "UC03-GATE", purpose, Guid.NewGuid(), mapId, false,
                parameters, _actorId, Now.AddDays(-1));
            Mission.Schedule(Now.AddHours(-1), Now.AddHours(1), Now.AddHours(-2));

            using var items = JsonDocument.Parse("""[{"key":"battery","prompt":"Battery safe?"}]""");
            using var answers = JsonDocument.Parse("""{"battery":true}""");
            var definition = PreflightChecklistDefinition.CreateActive(
                "DRONE_PRE_FLIGHT", 1, items, _actorId, Now.AddDays(-1));
            var operationId = Guid.NewGuid();
            var preflightActor = preflightByDifferentActor ? Guid.NewGuid() : _actorId;
            Mission.CompletePreflight(operationId, "v1", answers, suitableForFlight,
                "Safe to fly", preflightActor, Now.AddMinutes(-5));
            var checklist = MissionPreflightChecklist.Complete(Mission.Id,
                definition, operationId, answers, null,
                includeSafetyEvidence ? "Signal loss: land; low battery: return; interruption: abort" : null,
                preflightActor, Now.AddMinutes(-5), Now.AddMinutes(-5),
                includeSafetyEvidence ? "Local flight permission checked: reference UC03-1" : null);

            Planning = new PlanningQuery(new SurveyOrderMissionPlanningContext(
                orderId, _tenantId, FarmId, SurveyServiceType.PlantHealth,
                purpose == MissionPurpose.BaselineMapping, mapId, zoneIds,
                Now.AddHours(-1), Now.AddHours(1), true, null,
                true, true, boundaryId, _managerId));
            UnitOfWork = new UnitOfWork();
            Audit = new Audit();
            Maintenance = new MaintenanceRepository();
            Notes = new FieldNotesRepository();
            var checklistRepository = new ChecklistRepository(definition, checklist);
            Handler = new TransitionMissionCommandHandler(
                new MissionRepository(Mission), new DroneRepository(Drone),
                Maintenance, checklistRepository, Notes,
                new ManagerAccess(_tenantId, FarmId, _managerId), Planning,
                UnitOfWork, Audit, new Actor(_actorId), new FixedClock());
            RecoveryHandler = new RescheduleOrderMissionCommandHandler(
                new MissionRepository(Mission), Planning,
                new DroneQueries(Drone, ReplacementDrone),
                checklistRepository, Notes,
                new ManagerAccess(_tenantId, FarmId, _managerId), UnitOfWork,
                Audit, new Actor(_actorId), new FixedClock());
        }

        public Guid FarmId { get; }
        public DroneMission Mission { get; }
        public Drone Drone { get; }
        public Drone ReplacementDrone { get; }
        public PlanningQuery Planning { get; }
        public UnitOfWork UnitOfWork { get; }
        public Audit Audit { get; }
        public MaintenanceRepository Maintenance { get; }
        public FieldNotesRepository Notes { get; }
        public TransitionMissionCommandHandler Handler { get; }
        public RescheduleOrderMissionCommandHandler RecoveryHandler { get; }

        public Task<AgriDrone.SharedKernel.Application.Result<
            AgriDrone.Modules.Missions.Application.Features.Missions.MissionResponse>> Start() =>
            Handler.Handle(new TransitionMissionCommand(FarmId, Mission.Id,
                MissionStatus.InFlight, Mission.Version, null), CancellationToken.None);

        public Task<AgriDrone.SharedKernel.Application.Result<
            AgriDrone.Modules.Missions.Application.Features.Missions.MissionResponse>> FailFlight(
            string recoveryDecision) =>
            Handler.Handle(new TransitionMissionCommand(FarmId, Mission.Id,
                MissionStatus.FlightFailed, Mission.Version, "Safe landing after interruption",
                Guid.NewGuid(), "INTERRUPTION", "Landed safely",
                recoveryDecision, "media:incident-log"), CancellationToken.None);

        public Task<AgriDrone.SharedKernel.Application.Result<
            AgriDrone.Modules.Missions.Application.Features.Missions.MissionResponse>> Recover(
            Guid? replacementDroneId = null, uint? expectedVersion = null) =>
            RecoveryHandler.Handle(new RescheduleOrderMissionCommand(FarmId,
                Mission.Id, expectedVersion ?? Mission.Version,
                Now.AddMinutes(10), Now.AddMinutes(20),
                replacementDroneId),
                CancellationToken.None);
    }

    private sealed class PlanningQuery(SurveyOrderMissionPlanningContext context)
        : ISurveyOrderMissionPlanningQuery
    {
        public SurveyOrderMissionPlanningContext Context { get; set; } = context;
        public Task<SurveyOrderMissionPlanningContext?> GetAsync(Guid surveyOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyOrderMissionPlanningContext?>(Context);
    }

    private sealed class ManagerAccess(Guid tenantId, Guid farmId, Guid managerId)
        : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(Guid requestedFarmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SystemManagerFarmAccess.Allowed(tenantId, farmId,
                managerId));
    }

    private sealed class MissionRepository(DroneMission mission) : IDroneMissionRepository
    {
        public Task<DroneMission?> GetByIdAsync(Guid missionId, Guid tenantId,
            Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DroneMission?>(mission.Id == missionId &&
                mission.TenantId == tenantId && mission.FarmId == farmId ? mission : null);
        public Task<bool> CodeExistsAsync(Guid farmId, string missionCode,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
        public void Add(DroneMission value) => throw new NotSupportedException();
    }

    private sealed class DroneRepository(Drone drone) : IDroneRepository
    {
        public Task<Drone?> GetByIdAsync(Guid droneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Drone?>(drone.Id == droneId ? drone : null);
        public Task<bool> HasBlockingMissionAsync(Guid droneId,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> CodeExistsAsync(string code,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> SerialNumberExistsAsync(string serialNumber,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> RegistrationNumberExistsAsync(string registrationNumber,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
        public void Add(Drone value) => throw new NotSupportedException();
    }

    private sealed class DroneQueries(params Drone[] drones) : IDroneQueries
    {
        public Task<IReadOnlyList<AvailableDroneResponse>> GetAvailableAsync(
            DateTimeOffset startAt, DateTimeOffset endAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AvailableDroneResponse>>(
                drones.Where(drone => drone.IsOperationalFor(startAt, endAt))
                    .Select(drone => new AvailableDroneResponse(drone.Id, drone.Code, drone.Name,
                        drone.Model, drone.Manufacturer, drone.Specifications,
                        drone.RegistrationNumber, drone.RegistrationExpiryDate,
                        drone.WeightKg, drone.Status, drone.NextMaintenanceAt))
                    .ToArray());

        public Task<IReadOnlyList<DroneRegistryItemResponse>> GetRegistryAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DroneRegistryItemResponse>>([]);
    }

    private sealed class MaintenanceRepository : IDroneMaintenanceRepository
    {
        public List<DroneMaintenanceRecord> Records { get; } = [];
        public Task<DroneMaintenanceRecord?> GetOpenAsync(Guid droneId,
            CancellationToken cancellationToken) => Task.FromResult<DroneMaintenanceRecord?>(null);
        public Task<IReadOnlyList<DroneMaintenanceRecord>> ListAsync(Guid droneId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DroneMaintenanceRecord>>(Records);
        public void Add(DroneMaintenanceRecord record) => Records.Add(record);
    }

    private sealed class FieldNotesRepository : IMissionFieldNoteRepository
    {
        public List<MissionFieldNote> Items { get; } = [];
        public Task<MissionFieldNote?> GetByOperationIdAsync(Guid tenantId,
            Guid farmId, Guid missionId, Guid operationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(note =>
                note.TenantId == tenantId && note.FarmId == farmId &&
                note.MissionId == missionId && note.OperationId == operationId));
        public Task<IReadOnlyList<MissionFieldNote>> ListAsync(Guid tenantId,
            Guid farmId, Guid missionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MissionFieldNote>>(Items.Where(note =>
                note.TenantId == tenantId && note.FarmId == farmId &&
                note.MissionId == missionId).ToArray());
        public void Add(MissionFieldNote note) => Items.Add(note);
    }

    private sealed class ChecklistRepository(
        PreflightChecklistDefinition definition, MissionPreflightChecklist checklist)
        : IPreflightChecklistRepository
    {
        public Task<PreflightChecklistDefinition?> GetActiveDefinitionAsync(string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PreflightChecklistDefinition?>(definition);
        public Task<PreflightChecklistDefinition?> GetActiveDefinitionByIdAsync(
            Guid definitionId, string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<PreflightChecklistDefinition?>(definition);
        public Task<MissionPreflightChecklist?> GetByOperationIdAsync(Guid missionId,
            Guid operationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MissionPreflightChecklist?>(checklist.MissionId == missionId &&
                checklist.ClientOperationId == operationId ? checklist : null);
        public Task<int> GetNextVersionNumberAsync(string code,
            CancellationToken cancellationToken = default) => Task.FromResult(2);
        public Task RetireActiveDefinitionsAsync(string code, DateTimeOffset retiredAt,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void AddDefinition(PreflightChecklistDefinition value) =>
            throw new NotSupportedException();
        public void AddCompleted(MissionPreflightChecklist value) =>
            throw new NotSupportedException();
    }

    private sealed class UnitOfWork : IMissionsUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
        public void AddAuditLog(AuditLog value) { }
    }

    private sealed class Audit : IAuditWriter
    {
        public List<string> Actions { get; } = [];
        public void AddUserAction(IAuditLogSink sink, Guid tenantId, Guid? farmId,
            Guid actorId, Guid correlationId, string entityType, Guid entityId,
            string action, JsonDocument? oldData, JsonDocument? newData,
            DateTimeOffset createdAt) => Actions.Add(action);
        public void AddSystemAdminAction(IAuditLogSink sink, Guid actorId,
            Guid correlationId, string entityType, Guid entityId, string action,
            JsonDocument? oldData, JsonDocument? newData,
            DateTimeOffset createdAt) => throw new NotSupportedException();
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

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
