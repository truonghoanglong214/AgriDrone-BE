using System.Text.Json;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Application.Features.ArchiveFarm;
using AgriDrone.Modules.Farms.Application.Features.ArchiveZone;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Farms;

public sealed class ArchiveFarmZoneHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 8, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ArchiveZoneSoftArchivesAndCreatesAudit()
    {
        var fixture = CreateFixture();

        var result = await fixture.ZoneHandler.Handle(
            new ArchiveZoneCommand(
                fixture.Farm.Id,
                fixture.Zone.Id,
                fixture.Zone.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneralStatus.Inactive, fixture.Zone.Status);
        Assert.Equal(Now, fixture.Zone.DeletedAt);
        Assert.Equal(2, fixture.Zone.Version);
        Assert.Equal(1, fixture.ZoneRepository.UpdateCallCount);
        Assert.Equal(1, fixture.UnitOfWork.SaveCallCount);
        Assert.Equal(1, fixture.AuditWriter.UserActionCallCount);
        Assert.Equal(TenantAccessLevel.Owner, fixture.AccessService.RequiredAccess);
    }

    [Fact]
    public async Task ArchiveZoneRejectsNonOwner()
    {
        var fixture = CreateFixture();
        fixture.AccessService.Decision = AccessDecision.Deny(
            AccessDenialReason.TenantRoleInsufficient);

        var result = await fixture.ZoneHandler.Handle(
            new ArchiveZoneCommand(
                fixture.Farm.Id,
                fixture.Zone.Id,
                fixture.Zone.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmZone.AccessDenied", result.Error.Code);
        Assert.Equal(0, fixture.ZoneRepository.GetByIdCallCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task ArchiveZoneRejectsActiveDependencies()
    {
        var fixture = CreateFixture();
        fixture.DependencyQuery.ZoneDependencies = new(
            ActiveZoneCount: 0,
            ActiveMissionCount: 1,
            OpenFieldTaskCount: 2);

        var result = await fixture.ZoneHandler.Handle(
            new ArchiveZoneCommand(
                fixture.Farm.Id,
                fixture.Zone.Id,
                fixture.Zone.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FarmZone.ActiveDependenciesExist", result.Error.Code);
        Assert.Equal(0, fixture.ZoneRepository.UpdateCallCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task ArchiveFarmSoftArchivesAndCreatesAudit()
    {
        var fixture = CreateFixture();

        var result = await fixture.FarmHandler.Handle(
            new ArchiveFarmCommand(
                fixture.Farm.Id,
                fixture.Farm.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneralStatus.Inactive, fixture.Farm.Status);
        Assert.Equal(Now, fixture.Farm.DeletedAt);
        Assert.Equal(2, fixture.Farm.Version);
        Assert.Equal(1, fixture.FarmRepository.UpdateCallCount);
        Assert.Equal(1, fixture.UnitOfWork.SaveCallCount);
        Assert.Equal(1, fixture.AuditWriter.UserActionCallCount);
        Assert.Equal(TenantAccessLevel.Owner, fixture.AccessService.RequiredAccess);
    }

    [Fact]
    public async Task ArchiveFarmRejectsActiveDependencies()
    {
        var fixture = CreateFixture();
        fixture.DependencyQuery.FarmDependencies = new(
            ActiveZoneCount: 1,
            ActiveMissionCount: 0,
            OpenFieldTaskCount: 0);

        var result = await fixture.FarmHandler.Handle(
            new ArchiveFarmCommand(
                fixture.Farm.Id,
                fixture.Farm.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Farm.ActiveDependenciesExist", result.Error.Code);
        Assert.Equal(0, fixture.FarmRepository.UpdateCallCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task ArchiveFarmRejectsStaleExpectedVersion()
    {
        var fixture = CreateFixture();

        var result = await fixture.FarmHandler.Handle(
            new ArchiveFarmCommand(
                fixture.Farm.Id,
                ExpectedVersion: 99),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Farm.ConcurrentUpdate", result.Error.Code);
        Assert.Equal(0, fixture.DependencyQuery.FarmCallCount);
        Assert.Equal(0, fixture.UnitOfWork.SaveCallCount);
    }

    private static Fixture CreateFixture()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var farm = Farm.Create(
            tenantId,
            "FARM-001",
            "Test farm",
            address: null,
            boundary: null,
            centerPoint: null,
            areaHectares: 10,
            GeneralStatus.Active,
            actorId,
            Now.AddDays(-1));

        var zone = FarmZone.Create(
            farm.Id,
            "ZONE-001",
            "Test zone",
            boundary: null,
            areaHectares: 2,
            GeneralStatus.Active,
            actorId,
            Now.AddHours(-1));
        SetEntityId(zone, Guid.NewGuid());

        var farmRepository = new FakeFarmRepository(farm);
        var zoneRepository = new FakeFarmZoneRepository(zone);
        var unitOfWork = new FakeFarmUnitOfWork();
        var dependencyQuery = new FakeArchiveDependencyQuery();
        var auditWriter = new FakeAuditWriter();
        var executionContext = new FakeExecutionContext(tenantId, actorId);
        var accessService = new FakeEffectiveAccessService();
        var timeProvider = new FixedTimeProvider(Now);

        return new Fixture(
            new ArchiveFarmHandler(
                farmRepository,
                unitOfWork,
                dependencyQuery,
                auditWriter,
                executionContext,
                accessService,
                timeProvider),
            new ArchiveZoneHandler(
                zoneRepository,
                unitOfWork,
                dependencyQuery,
                auditWriter,
                executionContext,
                accessService,
                timeProvider),
            farm,
            zone,
            farmRepository,
            zoneRepository,
            unitOfWork,
            dependencyQuery,
            auditWriter,
            accessService);
    }

    private static void SetEntityId(Entity entity, Guid id) =>
        typeof(Entity<Guid>)
            .GetProperty(nameof(Entity.Id))!
            .SetValue(entity, id);

    private sealed record Fixture(
        ArchiveFarmHandler FarmHandler,
        ArchiveZoneHandler ZoneHandler,
        Farm Farm,
        FarmZone Zone,
        FakeFarmRepository FarmRepository,
        FakeFarmZoneRepository ZoneRepository,
        FakeFarmUnitOfWork UnitOfWork,
        FakeArchiveDependencyQuery DependencyQuery,
        FakeAuditWriter AuditWriter,
        FakeEffectiveAccessService AccessService);

    private sealed class FakeFarmRepository(Farm farm) : IFarmRepository
    {
        public int UpdateCallCount { get; private set; }

        public Task<Farm?> GetByIdAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Farm?>(farm);

        public Task<Farm?> GetByIdIncludingArchivedAsync(
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

        public void Update(Farm value) => UpdateCallCount++;
    }

    private sealed class FakeFarmZoneRepository(FarmZone zone)
        : IFarmZoneRepository
    {
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
            Task.FromResult(false);

        public void Add(FarmZone value) => throw new NotSupportedException();

        public void Update(FarmZone value) => UpdateCallCount++;
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

    private sealed class FakeArchiveDependencyQuery
        : IFarmArchiveDependencyQuery
    {
        public ArchiveDependencySummary ZoneDependencies { get; set; } =
            new(0, 0, 0);

        public ArchiveDependencySummary FarmDependencies { get; set; } =
            new(0, 0, 0);

        public int FarmCallCount { get; private set; }

        public Task<ArchiveDependencySummary> GetForZoneAsync(
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ZoneDependencies);

        public Task<ArchiveDependencySummary> GetForFarmAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default)
        {
            FarmCallCount++;
            return Task.FromResult(FarmDependencies);
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
        public TenantAccessLevel? RequiredAccess { get; private set; }

        public Task<AccessDecision> CheckTenantAsync(
            Guid actorId,
            Guid tenantId,
            TenantAccessLevel requiredAccess,
            CancellationToken cancellationToken = default)
        {
            RequiredAccess = requiredAccess;
            return Task.FromResult(Decision);
        }

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
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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
