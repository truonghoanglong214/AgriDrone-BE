using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;
using AgriDrone.Modules.Missions.Application.Features.Drones.RegisterDrone;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class SystemOwnedDroneAvailabilityTests
{
    private static readonly DateTimeOffset StartAt =
        new(2026, 9, 24, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AvailabilityRequiresActivePrimaryFarmAssignment()
    {
        var droneQueries = new RecordingDroneQueries();
        var handler = new GetAvailableDronesQueryHandler(
            droneQueries,
            new DeniedManagerAccessService());

        var result = await handler.Handle(
            new GetAvailableDronesQuery(
                Guid.NewGuid(),
                StartAt,
                StartAt.AddHours(1)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Drone.FarmAccessDenied", result.Error.Code);
        Assert.Equal(0, droneQueries.AvailabilityCallCount);
    }

    [Fact]
    public async Task AssignedManagerUsesGlobalAvailabilityQuery()
    {
        var farmId = Guid.NewGuid();
        var droneQueries = new RecordingDroneQueries();
        var handler = new GetAvailableDronesQueryHandler(
            droneQueries,
            new AllowedManagerAccessService(farmId));

        var result = await handler.Handle(
            new GetAvailableDronesQuery(
                farmId,
                StartAt,
                StartAt.AddHours(1)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, droneQueries.AvailabilityCallCount);
    }

    [Fact]
    public async Task RegistrationCreatesSystemScopedAuditWithoutTenant()
    {
        var repository = new RecordingDroneRepository();
        var unitOfWork = new RecordingMissionsUnitOfWork();
        var actorId = Guid.NewGuid();
        var handler = new RegisterDroneCommandHandler(
            repository,
            unitOfWork,
            new SystemAuditWriter(),
            new TestExecutionContext(actorId),
            new FixedTimeProvider(StartAt));

        var result = await handler.Handle(
            new RegisterDroneCommand(
                " sys-01 ",
                "Survey Drone",
                "Mavic",
                "DJI",
                Specifications: null,
                " serial-01 ",
                " vn-001 ",
                RegistrationDate: null,
                RegistrationExpiryDate: null,
                WeightKg: 1.2m,
                Notes: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SYS-01", result.Value.Code);
        Assert.Single(repository.Added);
        var audit = Assert.Single(unitOfWork.Audits);
        Assert.Null(audit.TenantId);
        Assert.Null(audit.FarmId);
        Assert.Equal("REGISTER", audit.Action);
    }

    private sealed class RecordingDroneQueries : IDroneQueries
    {
        public int AvailabilityCallCount { get; private set; }

        public Task<IReadOnlyList<AvailableDroneResponse>> GetAvailableAsync(
            DateTimeOffset startAt,
            DateTimeOffset endAt,
            CancellationToken cancellationToken = default)
        {
            AvailabilityCallCount++;
            return Task.FromResult<IReadOnlyList<AvailableDroneResponse>>([]);
        }

        public Task<IReadOnlyList<DroneRegistryItemResponse>> GetRegistryAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DroneRegistryItemResponse>>([]);
    }

    private sealed class DeniedManagerAccessService : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SystemManagerFarmAccess.Denied("Not assigned."));
    }

    private sealed class AllowedManagerAccessService(Guid expectedFarmId)
        : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(
            Guid farmId,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(expectedFarmId, farmId);
            return Task.FromResult(SystemManagerFarmAccess.Allowed(
                Guid.NewGuid(),
                farmId,
                Guid.NewGuid()));
        }
    }

    private sealed class RecordingDroneRepository : IDroneRepository
    {
        public List<Drone> Added { get; } = [];

        public Task<Drone?> GetByIdAsync(
            Guid droneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Drone?>(null);

        public Task<bool> CodeExistsAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> SerialNumberExistsAsync(
            string serialNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> RegistrationNumberExistsAsync(
            string registrationNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public void Add(Drone drone) => Added.Add(drone);
    }

    private sealed class RecordingMissionsUnitOfWork : IMissionsUnitOfWork
    {
        public List<AuditLog> Audits { get; } = [];

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public void AddAuditLog(AuditLog auditLog) => Audits.Add(auditLog);
    }

    private sealed class SystemAuditWriter : IAuditWriter
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
            throw new Xunit.Sdk.XunitException(
                "System-owned Drone registration must not use tenant audit.");

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

    private sealed class TestExecutionContext(Guid actorId) : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => Guid.NewGuid();
        public Guid? ActorId => actorId;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
