using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;
using AgriDrone.Modules.Missions.Application.Features.Telemetry.ImportTelemetry;
using AgriDrone.Modules.Missions.Application.Features.Telemetry.NormalizeTelemetryLog;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class NormalizeBlackboxPersistenceTests
{
    [Fact]
    public async Task SuccessfulDecodeReturnsSegmentsWithoutPersistingGps()
    {
        var fixture = new Fixture();
        var first = await fixture.Normalize();
        var second = await fixture.Normalize();

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(0, fixture.SaveCount);
        var point = first.Value.Segments[0].Points[0];
        Assert.Equal(3.0m, point.AltitudeM);
        Assert.Equal(10.461453, point.Latitude);
        Assert.Equal(106.493968, point.Longitude);
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 10, 12, 44,
            TimeSpan.Zero), point.RecordedAt);
    }

    [Fact]
    public async Task DecodeRejectsManagerWithoutFarmAccess()
    {
        var fixture = new Fixture { AllowFarmAccess = false };

        var result = await fixture.Normalize();

        Assert.True(result.IsFailure);
        Assert.Equal("MissionTelemetry.FarmAccessDenied", result.Error.Code);
        Assert.Equal(0, fixture.SaveCount);
    }

    private sealed class Fixture : IDroneMissionRepository,
        IMissionTelemetryRepository, IMissionsUnitOfWork,
        ITelemetryLogNormalizer, IExecutionContext, ISystemManagerAccessService
    {
        private readonly DroneMission mission;
        private readonly NormalizeTelemetryLogCommandHandler handler;
        public int SaveCount;
        public bool AllowFarmAccess { get; set; } = true;
        public Guid? TenantId => null;
        public Guid? ActorId { get; } = Guid.NewGuid();
        public bool IsInitialized => true;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => default;

        public Fixture()
        {
            using var parameters = JsonDocument.Parse("{}");
            mission = DroneMission.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), null, "BLACKBOX-TEST", MissionType.Mapping,
                null, parameters, null, ActorId!.Value, DateTimeOffset.UtcNow);
            handler = new(this, this, this, this);
        }

        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AllowFarmAccess && farmId == mission.FarmId
                ? SystemManagerFarmAccess.Allowed(mission.TenantId,
                    mission.FarmId, Guid.NewGuid())
                : SystemManagerFarmAccess.Denied("Farm access denied."));

        public async Task<Result<NormalizedTelemetryLog>> Normalize()
        {
            using var stream = new MemoryStream([1]);
            return await handler.Handle(new NormalizeTelemetryLogCommand(
                mission.FarmId, mission.Id, "flight.bbl", stream),
                CancellationToken.None);
        }

        public Task<Result<NormalizedTelemetryLog>> NormalizeAsync(
            Stream content, string sourceFileName,
            CancellationToken cancellationToken = default)
        {
            var timestamp = new DateTimeOffset(2026, 10, 13, 10, 12, 44,
                TimeSpan.Zero);
            var points = new[]
            {
                new ImportTelemetryPoint(0, timestamp, 106.493968,
                    10.461453, 3.0m, null, null, null, null),
                new ImportTelemetryPoint(1, timestamp.AddSeconds(1),
                    106.493969, 10.461454, 3.1m, null, null, null, null)
            };
            return Task.FromResult(Result.Success(new NormalizedTelemetryLog(
                sourceFileName, new string('a', 64),
                [new NormalizedTelemetrySegment(0, 2, 0, points, [])], [])));
        }

        public Task<DroneMission?> GetByIdAsync(Guid missionId, Guid tenantId,
            Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DroneMission?>(
                missionId == mission.Id && tenantId == mission.TenantId &&
                farmId == mission.FarmId ? mission : null);
        public Task<bool> CodeExistsAsync(Guid farmId, string missionCode,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
        public void Add(DroneMission value) => throw new NotSupportedException();

        public Task<MissionTelemetryImport?> GetImportByOperationIdAsync(
            Guid tenantId, Guid farmId, Guid missionId, Guid operationId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<MissionTelemetryImport?> GetImportByMissionAsync(
            Guid tenantId, Guid farmId, Guid missionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public void Add(MissionTelemetryImport telemetryImport,
            IReadOnlyCollection<MissionTelemetryPoint> points) =>
            throw new NotSupportedException();
        public Task ReplaceAsync(MissionTelemetryImport telemetryImport,
            IReadOnlyCollection<MissionTelemetryPoint> points,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
        public void AddAuditLog(AuditLog auditLog) =>
            throw new NotSupportedException();
    }
}
