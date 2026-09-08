using System.Text.Json;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Features.UpdateZone;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Farms;

public sealed class UpdateZoneHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 8, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleUpdatesZoneAndCreatesAudit()
    {
        var fixture = CreateFixture();
        var updatedBoundary = CreatePolygon(2, 2, 4, 4);

        var result = await fixture.Handler.Handle(
            new UpdateZoneCommand(
                fixture.Farm.Id,
                fixture.Zone.Id,
                " Updated zone ",
                updatedBoundary,
                2.5m,
                fixture.Zone.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ZONE-001", result.Value.Code);
        Assert.Equal("Updated zone", result.Value.Name);
        Assert.Equal(2.5m, result.Value.AreaHectares);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(Now, result.Value.UpdatedAt);
        Assert.Equal(1, fixture.ZoneRepository.UpdateCallCount);
        Assert.Equal(1, fixture.UnitOfWork.SaveCallCount);
        Assert.Equal(1, fixture.AuditWriter.UserActionCallCount);
        Assert.Equal(FarmAccessLevel.Manager, fixture.AccessService.RequiredAccess);
        Assert.Equal(fixture.Zone.Id, fixture.AccessService.ZoneId);
    }

    [Fact]
    public async Task HandleRejectsActorWithoutZoneManagerAccess()
    {
        var fixture = CreateFixture();
        fixture.AccessService.Decision = AccessDecision.Deny(
            AccessDenialReason.ZoneAssignmentNotActive);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmZone.AccessDenied", result.Error.Code);
        Assert.Equal(0, fixture.ZoneRepository.GetByIdCallCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task HandleRejectsBoundaryOutsideFarm()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            CreateCommand(
                fixture,
                boundary: CreatePolygon(9, 9, 12, 12)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmZone.BoundaryOutsideFarm", result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task HandleRejectsBoundaryOverlappingAnotherZone()
    {
        var fixture = CreateFixture();
        fixture.ZoneRepository.BoundaryOverlaps = true;

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmZone.BoundaryOverlaps", result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task HandleReturnsCurrentZoneWhenDetailsAreUnchanged()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            new UpdateZoneCommand(
                fixture.Farm.Id,
                fixture.Zone.Id,
                fixture.Zone.Name,
                fixture.Zone.Boundary,
                fixture.Zone.AreaHectares,
                fixture.Zone.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(0, fixture.ZoneRepository.UpdateCallCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCallCount);
        Assert.Equal(0, fixture.AuditWriter.UserActionCallCount);
    }

    [Fact]
    public async Task HandleRejectsStaleExpectedVersion()
    {
        var fixture = CreateFixture();
        var command = CreateCommand(fixture) with { ExpectedVersion = 99 };

        var result = await fixture.Handler.Handle(
            command,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmZone.ConcurrentUpdate", result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveCallCount);
    }

    private static UpdateZoneCommand CreateCommand(
        Fixture fixture,
        Polygon? boundary = null) =>
        new(
            fixture.Farm.Id,
            fixture.Zone.Id,
            "Updated zone",
            boundary ?? CreatePolygon(2, 2, 4, 4),
            2.5m,
            fixture.Zone.Version);

    private static Fixture CreateFixture()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var farm = Farm.Create(
            tenantId,
            "FARM-001",
            "Test farm",
            address: null,
            CreatePolygon(0, 0, 10, 10),
            centerPoint: null,
            areaHectares: 100,
            GeneralStatus.Active,
            actorId,
            Now.AddDays(-1));

        var zone = FarmZone.Create(
            farm.Id,
            "ZONE-001",
            "Test zone",
            CreatePolygon(1, 1, 3, 3),
            areaHectares: 1.5m,
            GeneralStatus.Active,
            actorId,
            Now.AddHours(-1));
        SetEntityId(zone, Guid.NewGuid());

        var zoneRepository = new FakeFarmZoneRepository(zone);
        var unitOfWork = new FakeFarmUnitOfWork();
        var auditWriter = new FakeAuditWriter();
        var accessService = new FakeEffectiveAccessService();
        var handler = new UpdateZoneHandler(
            zoneRepository,
            new FakeFarmRepository(farm),
            unitOfWork,
            auditWriter,
            new FakeExecutionContext(tenantId, actorId),
            accessService,
            new FixedTimeProvider(Now));

        return new Fixture(
            handler,
            farm,
            zone,
            zoneRepository,
            unitOfWork,
            auditWriter,
            accessService);
    }

    private static Polygon CreatePolygon(
        double minX,
        double minY,
        double maxX,
        double maxY)
    {
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        return factory.CreatePolygon(
        [
            new Coordinate(minX, minY),
            new Coordinate(maxX, minY),
            new Coordinate(maxX, maxY),
            new Coordinate(minX, maxY),
            new Coordinate(minX, minY)
        ]);
    }

    private static void SetEntityId(Entity entity, Guid id) =>
        typeof(Entity<Guid>)
            .GetProperty(nameof(Entity.Id))!
            .SetValue(entity, id);

    private sealed record Fixture(
        UpdateZoneHandler Handler,
        Farm Farm,
        FarmZone Zone,
        FakeFarmZoneRepository ZoneRepository,
        FakeFarmUnitOfWork UnitOfWork,
        FakeAuditWriter AuditWriter,
        FakeEffectiveAccessService AccessService);

    private sealed class FakeFarmZoneRepository(FarmZone zone)
        : IFarmZoneRepository
    {
        public bool BoundaryOverlaps { get; set; }
        public int GetByIdCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }

        public Task<FarmZone?> GetByIdAsync(
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            return Task.FromResult<FarmZone?>(zone);
        }

        public Task<FarmZone?> GetByCodeAsync(
            Guid tenantId,
            Guid farmId,
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<FarmZone?>(null);

        public Task<bool> ActiveCodeExistsAsync(
            Guid tenantId,
            Guid farmId,
            string code,
            Guid? excludingZoneId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> ActiveBoundaryOverlapsAsync(
            Guid tenantId,
            Guid farmId,
            Polygon boundary,
            Guid? excludingZoneId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(BoundaryOverlaps);

        public void Add(FarmZone farmZone) =>
            throw new NotSupportedException();

        public void Update(FarmZone farmZone) => UpdateCallCount++;
    }

    private sealed class FakeFarmRepository(Farm farm) : IFarmRepository
    {
        public Task<Farm?> GetByIdAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Farm?>(farm);

        public Task<Farm?> GetByCodeAsync(
            Guid tenantId,
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Farm?>(null);

        public Task<bool> ActiveCodeExistsAsync(
            Guid tenantId,
            string code,
            Guid? excludingFarmId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public void Add(Farm value) => throw new NotSupportedException();
        public void Update(Farm value) => throw new NotSupportedException();
    }

    private sealed class FakeFarmUnitOfWork : IFarmUnitOfWork
    {
        public int SaveCallCount { get; private set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return Task.FromResult(1);
        }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public void AddAuditLog(AuditLog auditLog)
        {
        }
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public int UserActionCallCount { get; private set; }

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
            UserActionCallCount++;

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
            throw new NotSupportedException();
    }

    private sealed class FakeEffectiveAccessService : IEffectiveAccessService
    {
        public AccessDecision Decision { get; set; } = AccessDecision.Allow();
        public FarmAccessLevel? RequiredAccess { get; private set; }
        public Guid? ZoneId { get; private set; }

        public Task<AccessDecision> CheckTenantAsync(
            Guid actorId,
            Guid tenantId,
            TenantAccessLevel requiredAccess,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AccessDecision> CheckFarmAsync(
            Guid actorId,
            Guid tenantId,
            Guid farmId,
            FarmAccessLevel requiredAccess,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AccessDecision> CheckZoneAsync(
            Guid actorId,
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            FarmAccessLevel requiredAccess,
            CancellationToken cancellationToken = default)
        {
            RequiredAccess = requiredAccess;
            ZoneId = zoneId;
            return Task.FromResult(Decision);
        }
    }

    private sealed class FakeExecutionContext(Guid tenantId, Guid actorId)
        : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => tenantId;
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
