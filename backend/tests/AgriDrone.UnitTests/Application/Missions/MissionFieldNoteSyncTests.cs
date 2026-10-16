using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Features.Missions.GetFieldNotes;
using AgriDrone.Modules.Missions.Application.Features.Missions.SyncFieldNote;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class MissionFieldNoteSyncTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 9, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OfflineRetryReturnsOriginalNoteWithoutSecondAudit()
    {
        var fixture = new Fixture();
        var operationId = Guid.NewGuid();
        var command = new SyncFieldNoteCommand(fixture.FarmId,
            fixture.Mission.Id, operationId, "  Wind shifted  ", Now.AddMinutes(-10));

        var first = await fixture.Handler.Handle(command, CancellationToken.None);
        var retry = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(first.Value.ReusedOperation);
        Assert.True(retry.Value.ReusedOperation);
        Assert.Equal(first.Value.NoteId, retry.Value.NoteId);
        Assert.Single(fixture.Notes.Items);
        Assert.Single(fixture.UnitOfWork.Audits);
        Assert.Equal(1, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReusingOperationWithDifferentContentIsConflict()
    {
        var fixture = new Fixture();
        var operationId = Guid.NewGuid();
        await fixture.Handler.Handle(new SyncFieldNoteCommand(fixture.FarmId,
            fixture.Mission.Id, operationId, "Wind shifted", Now), CancellationToken.None);

        var conflicting = await fixture.Handler.Handle(new SyncFieldNoteCommand(
            fixture.FarmId, fixture.Mission.Id, operationId, "Rain started", Now),
            CancellationToken.None);

        Assert.True(conflicting.IsFailure);
        Assert.Equal("MissionFieldNote.OperationPayloadConflict", conflicting.Error.Code);
        Assert.Single(fixture.Notes.Items);
    }

    [Fact]
    public async Task UnassignedManagerCannotWriteOrReadNotes()
    {
        var fixture = new Fixture(allowAccess: false);
        var write = await fixture.Handler.Handle(new SyncFieldNoteCommand(
            fixture.FarmId, fixture.Mission.Id, Guid.NewGuid(), "Observation", Now),
            CancellationToken.None);
        var read = await fixture.ReadHandler.Handle(new GetFieldNotesQuery(
            fixture.FarmId, fixture.Mission.Id), CancellationToken.None);

        Assert.True(write.IsFailure);
        Assert.True(read.IsFailure);
        Assert.Equal("MissionFieldNote.FarmAccessDenied", write.Error.Code);
        Assert.Equal("MissionFieldNote.FarmAccessDenied", read.Error.Code);
        Assert.Empty(fixture.Notes.Items);
    }

    [Fact]
    public async Task FutureObservationIsRejectedWithoutSaving()
    {
        var fixture = new Fixture();
        var result = await fixture.Handler.Handle(new SyncFieldNoteCommand(
            fixture.FarmId, fixture.Mission.Id, Guid.NewGuid(), "Observation",
            Now.AddMinutes(6)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionFieldNote.FutureObservation", result.Error.Code);
        Assert.Empty(fixture.Notes.Items);
    }

    [Theory]
    [InlineData("SIGNAL_LOSS")]
    [InlineData("LOW_BATTERY")]
    [InlineData("INTERRUPTION")]
    public async Task FlightIncidentRetainsEvidenceActorAndOutcome(string incidentType)
    {
        var fixture = new Fixture();
        using var answers = JsonDocument.Parse("""{"battery":true}""");
        fixture.Mission.CompletePreflight(Guid.NewGuid(), "v1", answers,
            true, "Ready", fixture.ActorId, Now.AddMinutes(-5));
        fixture.Mission.StartFlight(fixture.ActorId, Now.AddMinutes(-4));
        var command = new SyncFieldNoteCommand(fixture.FarmId, fixture.Mission.Id,
            Guid.NewGuid(), "Safety observation", Now.AddMinutes(-2),
            incidentType, "Safe landing procedure checked", "CONTINUE",
            "media:flight-log-123");

        var saved = await fixture.Handler.Handle(command, CancellationToken.None);
        var replay = await fixture.Handler.Handle(command, CancellationToken.None);
        var listed = await fixture.ReadHandler.Handle(new GetFieldNotesQuery(
            fixture.FarmId, fixture.Mission.Id), CancellationToken.None);

        Assert.True(saved.IsSuccess);
        Assert.True(replay.Value.ReusedOperation);
        Assert.Single(fixture.Notes.Items);
        var item = Assert.Single(listed.Value);
        Assert.Equal(incidentType, item.IncidentType);
        Assert.Equal("Safe landing procedure checked", item.IncidentOutcome);
        Assert.Equal("CONTINUE", item.RecoveryDecision);
        Assert.Equal("media:flight-log-123", item.EvidenceReference);
        Assert.Equal(fixture.ActorId, item.CreatedBy);
    }

    [Fact]
    public async Task RescheduleDecisionRequiresFailedFlight()
    {
        var fixture = new Fixture();
        var result = await fixture.Handler.Handle(new SyncFieldNoteCommand(
            fixture.FarmId, fixture.Mission.Id, Guid.NewGuid(), "Low battery",
            Now, "LOW_BATTERY", "Returned safely", "RESCHEDULE_REQUIRED",
            "media:log-456"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionFieldNote.IncidentStatus", result.Error.Code);
        Assert.Empty(fixture.Notes.Items);
    }

    [Fact]
    public async Task IncidentDetailsWithoutTypeAreRejectedByHandler()
    {
        var fixture = new Fixture();
        var result = await fixture.Handler.Handle(new SyncFieldNoteCommand(
            fixture.FarmId, fixture.Mission.Id, Guid.NewGuid(),
            "Observation", Now, null, "Outcome", "CONTINUE",
            "media:log-789"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionFieldNote.InvalidIncident", result.Error.Code);
        Assert.Empty(fixture.Notes.Items);
    }

    private sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid FarmId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public DroneMission Mission { get; }
        public RecordingNotes Notes { get; } = new();
        public RecordingUnitOfWork UnitOfWork { get; } = new();
        public SyncFieldNoteCommandHandler Handler { get; }
        public GetFieldNotesQueryHandler ReadHandler { get; }

        public Fixture(bool allowAccess = true)
        {
            using var parameters = JsonDocument.Parse("{}");
            Mission = DroneMission.CreateOrderBound(Guid.NewGuid(), TenantId,
                FarmId, [], Guid.NewGuid(), Guid.NewGuid(), ActorId, "FIELD-NOTE-TEST",
                MissionPurpose.BaselineMapping, Guid.NewGuid(), null,
                false, parameters, ActorId, Now.AddHours(-1));
            Mission.Schedule(Now.AddHours(-1), Now.AddHours(1), Now.AddHours(-1));
            var missionRepository = new RecordingMissions(Mission);
            var access = new Access(TenantId, FarmId, allowAccess);
            var execution = new ActorContext(ActorId);
            Handler = new SyncFieldNoteCommandHandler(missionRepository, Notes,
                access, UnitOfWork, new RecordingAuditWriter(), execution,
                new FixedClock());
            ReadHandler = new GetFieldNotesQueryHandler(missionRepository,
                Notes, access, execution);
        }
    }

    private sealed class RecordingNotes : IMissionFieldNoteRepository
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

    private sealed class RecordingMissions(DroneMission mission) : IDroneMissionRepository
    {
        public Task<DroneMission?> GetByIdAsync(Guid missionId, Guid tenantId,
            Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DroneMission?>(mission.Id == missionId &&
                mission.TenantId == tenantId && mission.FarmId == farmId
                ? mission : null);

        public Task<bool> CodeExistsAsync(Guid farmId, string missionCode,
            CancellationToken cancellationToken = default) => Task.FromResult(false);

        public void Add(DroneMission value) => throw new NotSupportedException();
    }

    private sealed class Access(Guid tenantId, Guid expectedFarmId, bool allowed)
        : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(allowed && farmId == expectedFarmId
                ? SystemManagerFarmAccess.Allowed(tenantId, farmId, Guid.NewGuid())
                : SystemManagerFarmAccess.Denied("Not assigned."));
    }

    private sealed class ActorContext(Guid actorId) : IExecutionContext
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

    private sealed class RecordingUnitOfWork : IMissionsUnitOfWork
    {
        public int SaveCount { get; private set; }
        public List<AuditLog> Audits { get; } = [];
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
        public void AddAuditLog(AuditLog auditLog) => Audits.Add(auditLog);
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public void AddUserAction(IAuditLogSink sink, Guid tenantId,
            Guid? farmId, Guid actorId, Guid correlationId, string entityType,
            Guid entityId, string action, JsonDocument? oldData,
            JsonDocument? newData, DateTimeOffset createdAt) =>
            sink.AddAuditLog(AuditLog.ForUserAction(tenantId, farmId, actorId,
                correlationId, entityType, entityId, action, oldData,
                newData, createdAt));

        public void AddSystemAdminAction(IAuditLogSink sink, Guid actorId,
            Guid correlationId, string entityType, Guid entityId, string action,
            JsonDocument? oldData, JsonDocument? newData, DateTimeOffset createdAt) =>
            throw new NotSupportedException();
    }
}
