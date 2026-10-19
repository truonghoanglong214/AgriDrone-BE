using System.Text.Json;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Application.Features.VerifyFarmBoundary;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Farms;

public sealed class VerifyFarmBoundaryCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
    private readonly GeometryFactory _geometryFactory = new(new PrecisionModel(), 4326);

    [Fact]
    public async Task HandleReturnsFailureWhenActorNotAuthenticated()
    {
        var fixture = CreateFixture();
        fixture.ExecutionContext.ActorIdValue = null;

        var command = new VerifyFarmBoundaryCommand(
            fixture.Farm.Id,
            fixture.ValidBoundaryPolygon,
            "Approval reason",
            "evidence-01",
            fixture.Farm.Version,
            ExpectedCurrentBoundaryVersionNumber: null);

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationError.CurrentUserRequired().Code, result.Error.Code);
    }

    [Fact]
    public async Task HandleReturnsAccessDeniedWhenAccessDenied()
    {
        var fixture = CreateFixture();
        fixture.AccessService.Decision = SystemManagerFarmAccess.Denied("Not authorized for this farm");

        var command = new VerifyFarmBoundaryCommand(
            fixture.Farm.Id,
            fixture.ValidBoundaryPolygon,
            "Approval reason",
            "evidence-01",
            fixture.Farm.Version,
            ExpectedCurrentBoundaryVersionNumber: null);

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Farm.AccessDenied", result.Error.Code);
    }

    [Fact]
    public async Task HandleReturnsNotFoundWhenFarmNotFound()
    {
        var fixture = CreateFixture();
        fixture.FarmRepository.Farm = null;

        var command = new VerifyFarmBoundaryCommand(
            fixture.Farm.Id,
            fixture.ValidBoundaryPolygon,
            "Approval reason",
            "evidence-01",
            fixture.Farm.Version,
            ExpectedCurrentBoundaryVersionNumber: null);

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Farm.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task HandleReturnsConcurrentUpdateWhenFarmVersionMismatch()
    {
        var fixture = CreateFixture();

        var command = new VerifyFarmBoundaryCommand(
            fixture.Farm.Id,
            fixture.ValidBoundaryPolygon,
            "Approval reason",
            "evidence-01",
            ExpectedFarmVersion: 999,
            ExpectedCurrentBoundaryVersionNumber: null);

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Farm.ConcurrentUpdate", result.Error.Code);
    }

    [Fact]
    public async Task HandleReturnsBoundaryConcurrentModificationWhenBoundaryVersionMismatch()
    {
        var fixture = CreateFixture();
        var existingBoundary = FarmBoundary.CreateDraft(
            fixture.TenantId,
            fixture.Farm.Id,
            versionNumber: 2,
            fixture.ValidBoundaryPolygon,
            FarmBoundarySource.SystemManager,
            sourceSurveyRequestId: null,
            submittedBy: fixture.ActorId,
            createdAt: Now);
        existingBoundary.Approve(fixture.ActorId, "Approved v2", Now);
        fixture.BoundaryRepository.CurrentApproved = existingBoundary;

        var command = new VerifyFarmBoundaryCommand(
            fixture.Farm.Id,
            fixture.ValidBoundaryPolygon,
            "Approval reason",
            "evidence-01",
            fixture.Farm.Version,
            ExpectedCurrentBoundaryVersionNumber: 1);

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(FarmError.BoundaryConcurrentModification().Code, result.Error.Code);
    }

    [Fact]
    public async Task HandleCreatesAndApprovesNewBoundaryAndSupersedesCurrentBoundaryWhenValid()
    {
        var fixture = CreateFixture();
        var existingBoundary = FarmBoundary.CreateDraft(
            fixture.TenantId,
            fixture.Farm.Id,
            versionNumber: 1,
            fixture.ValidBoundaryPolygon,
            FarmBoundarySource.SystemManager,
            sourceSurveyRequestId: null,
            submittedBy: fixture.ActorId,
            createdAt: Now);
        existingBoundary.Approve(fixture.ActorId, "Approved v1", Now);
        fixture.BoundaryRepository.CurrentApproved = existingBoundary;
        fixture.BoundaryRepository.NextVersion = 2;

        var command = new VerifyFarmBoundaryCommand(
            fixture.Farm.Id,
            fixture.ValidBoundaryPolygon,
            "Approval reason v2",
            "evidence-02",
            fixture.Farm.Version,
            ExpectedCurrentBoundaryVersionNumber: 1);

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.VersionNumber);
        Assert.Equal("Approved", result.Value.Status);
        Assert.Equal("evidence-02", result.Value.EvidenceReference);
        Assert.Equal("Approval reason v2", result.Value.ReviewReason);

        Assert.Equal(FarmBoundaryStatus.Superseded, existingBoundary.Status);
        Assert.Equal(1, fixture.BoundaryRepository.AddCallCount);
        Assert.Equal(1, fixture.BoundaryRepository.UpdateCallCount);
        Assert.Equal(1, fixture.UnitOfWork.SaveCallCount);
        Assert.Equal(1, fixture.AuditWriter.UserActionCallCount);
    }

    private TestFixture CreateFixture()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var farmId = Guid.NewGuid();

        var boundaryPolygon = _geometryFactory.CreatePolygon([
            new Coordinate(106.0, 10.0),
            new Coordinate(106.1, 10.0),
            new Coordinate(106.1, 10.1),
            new Coordinate(106.0, 10.1),
            new Coordinate(106.0, 10.0)
        ]);
        boundaryPolygon.SRID = 4326;

        var farm = Farm.Create(
            tenantId,
            "FARM-TEST-01",
            "Test Farm",
            "Test Address",
            boundaryPolygon,
            centerPoint: null,
            areaHectares: 10m,
            GeneralStatus.Active,
            actorId,
            Now);

        var farmRepo = new FakeFarmRepository { Farm = farm };
        var boundaryRepo = new FakeFarmBoundaryRepository();
        var unitOfWork = new FakeFarmUnitOfWork();
        var auditWriter = new FakeAuditWriter();
        var execContext = new FakeExecutionContext(tenantId, actorId);
        var accessService = new FakeSystemManagerAccessService(tenantId, farmId);
        var timeProvider = new FixedTimeProvider(Now);

        var handler = new VerifyFarmBoundaryCommandHandler(
            farmRepo,
            boundaryRepo,
            unitOfWork,
            auditWriter,
            execContext,
            accessService,
            timeProvider);

        return new TestFixture(
            tenantId,
            actorId,
            farm,
            boundaryPolygon,
            farmRepo,
            boundaryRepo,
            unitOfWork,
            auditWriter,
            execContext,
            accessService,
            handler);
    }

    private sealed record TestFixture(
        Guid TenantId,
        Guid ActorId,
        Farm Farm,
        Polygon ValidBoundaryPolygon,
        FakeFarmRepository FarmRepository,
        FakeFarmBoundaryRepository BoundaryRepository,
        FakeFarmUnitOfWork UnitOfWork,
        FakeAuditWriter AuditWriter,
        FakeExecutionContext ExecutionContext,
        FakeSystemManagerAccessService AccessService,
        VerifyFarmBoundaryCommandHandler Handler);

    private sealed class FakeFarmRepository : IFarmRepository
    {
        public Farm? Farm { get; set; }

        public Task<Farm?> GetByIdAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
            => Task.FromResult(Farm);

        public Task<Farm?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default)
            => Task.FromResult(Farm);

        public Task<bool> ActiveCodeExistsAsync(Guid tenantId, string code, Guid? excludingFarmId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<Farm?> GetByIdIncludingArchivedAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
            => Task.FromResult(Farm);

        public void Add(Farm farm) { }
        public void Update(Farm farm) { }
    }

    private sealed class FakeFarmBoundaryRepository : IFarmBoundaryRepository
    {
        public FarmBoundary? CurrentApproved { get; set; }
        public int NextVersion { get; set; } = 1;
        public int AddCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }

        public Task<FarmBoundary?> GetCurrentApprovedAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentApproved);

        public Task<FarmBoundary?> GetByIdAsync(Guid tenantId, Guid farmId, Guid boundaryId, CancellationToken cancellationToken = default)
            => Task.FromResult<FarmBoundary?>(null);

        public Task<int> GetNextVersionNumberAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
            => Task.FromResult(NextVersion);

        public void Add(FarmBoundary boundary) => AddCallCount++;
        public void Update(FarmBoundary boundary) => UpdateCallCount++;
    }

    private sealed class FakeFarmUnitOfWork : IFarmUnitOfWork
    {
        public int SaveCallCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return Task.FromResult(1);
        }

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);

        public void AddAuditLog(AuditLog auditLog) { }
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

    private sealed class FakeSystemManagerAccessService(Guid tenantId, Guid assignedFarmId) : ISystemManagerAccessService
    {
        public SystemManagerFarmAccess Decision { get; set; } =
            SystemManagerFarmAccess.Allowed(tenantId, assignedFarmId, Guid.NewGuid());

        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(Guid farmId, CancellationToken cancellationToken = default)
            => Task.FromResult(Decision);
    }

    private sealed class FakeExecutionContext(Guid tenantId, Guid actorId) : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => tenantId;
        public Guid? ActorId { get; set; } = actorId;
        public Guid? ActorIdValue
        {
            get => ActorId;
            set => ActorId = value;
        }
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
