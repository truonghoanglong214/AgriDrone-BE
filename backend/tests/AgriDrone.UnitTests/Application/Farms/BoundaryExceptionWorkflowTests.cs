using System.Text.Json;
using AgriDrone.Modules.Farms.Application.Abstractions.Boundaries;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.Farms.Application.Features.ClassifyAndRecordPoint;
using AgriDrone.Modules.Farms.Application.Features.GetBoundaryExceptions;
using AgriDrone.Modules.Farms.Application.Features.ReviewBoundaryException;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Infrastructure.Boundaries;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Farms;

public sealed class BoundaryExceptionWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
    private readonly GeometryFactory _geometryFactory = new(new PrecisionModel(), 4326);

    [Fact]
    public async Task ClassifyAndRecordPointWhenPointIsWithinBoundaryDoesNotCreateException()
    {
        var fixture = CreateFixture();
        var pointInside = _geometryFactory.CreatePoint(new Coordinate(106.05, 10.05));
        var command = new ClassifyAndRecordPointCommand(
            fixture.Farm.Id,
            pointInside,
            BoundaryExceptionSource.Manual,
            "tel-001");

        var result = await fixture.ClassifyHandler.Handle(
            new ClassifyAndRecordPointRequest(command),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PointBoundaryClassification.WithinBoundary, result.Value.Classification);
        Assert.Null(result.Value.BoundaryExceptionId);
        Assert.Null(result.Value.State);
        Assert.Equal(0, fixture.ExceptionRepo.AddCallCount);
    }

    [Fact]
    public async Task ClassifyAndRecordPointWhenPointIsNearBoundaryCreatesNeedsReviewException()
    {
        var fixture = CreateFixture();
        // Just slightly outside boundary, within 5m
        // 1 deg ~ 111,320m, so 0.00001 deg ~ 1.1m
        var pointNear = _geometryFactory.CreatePoint(new Coordinate(106.10001, 10.05));
        var command = new ClassifyAndRecordPointCommand(
            fixture.Farm.Id,
            pointNear,
            BoundaryExceptionSource.BaselineCandidate,
            "viol-001",
            SurveyOrderId: Guid.NewGuid(),
            MissionId: Guid.NewGuid(),
            NearBoundaryThresholdMeters: 5.0);

        var result = await fixture.ClassifyHandler.Handle(
            new ClassifyAndRecordPointRequest(command),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PointBoundaryClassification.NearBoundary, result.Value.Classification);
        Assert.NotNull(result.Value.BoundaryExceptionId);
        Assert.Equal(BoundaryExceptionState.NeedsReview.ToString(), result.Value.State);
        Assert.Equal(1, fixture.ExceptionRepo.AddCallCount);
        Assert.Equal(1, fixture.UnitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task ClassifyAndRecordPointWhenPointIsOutOfBoundaryCreatesOutOfBoundaryException()
    {
        var fixture = CreateFixture();
        // Far outside (0.1 deg ~ 11km)
        var pointFar = _geometryFactory.CreatePoint(new Coordinate(106.2, 10.05));
        var command = new ClassifyAndRecordPointCommand(
            fixture.Farm.Id,
            pointFar,
            BoundaryExceptionSource.BaselineCandidate,
            "viol-002",
            SurveyOrderId: Guid.NewGuid(),
            MissionId: Guid.NewGuid());

        var result = await fixture.ClassifyHandler.Handle(
            new ClassifyAndRecordPointRequest(command),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PointBoundaryClassification.OutOfBoundary, result.Value.Classification);
        Assert.NotNull(result.Value.BoundaryExceptionId);
        Assert.Equal(BoundaryExceptionState.OutOfBoundary.ToString(), result.Value.State);
        Assert.Equal(1, fixture.ExceptionRepo.AddCallCount);
    }

    [Fact]
    public async Task ReviewBoundaryExceptionSuccessfullyResolvesException()
    {
        var fixture = CreateFixture();
        var point = _geometryFactory.CreatePoint(new Coordinate(106.10001, 10.05));
        var exception = BoundaryException.Create(
            fixture.TenantId,
            fixture.Farm.Id,
            fixture.ApprovedBoundary.Id,
            BoundaryExceptionSource.BaselineCandidate,
            "viol-001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            point,
            BoundaryExceptionState.NeedsReview,
            1.1m,
            5.0m,
            "1.0",
            Now.AddMinutes(-10));

        fixture.ExceptionRepo.Store[exception.Id] = exception;

        using var evidenceDoc = JsonDocument.Parse("{\"notes\":\"pilot inspected site\"}");
        var reviewCommand = new ReviewBoundaryExceptionCommand(
            fixture.Farm.Id,
            exception.Id,
            BoundaryExceptionDecision.RejectedOutside,
            CorrectedPosition: null,
            Reason: "Point confirmed outside boundary",
            Evidence: evidenceDoc,
            ExpectedVersion: exception.Version);

        var result = await fixture.ReviewHandler.Handle(
            new ReviewBoundaryExceptionRequest(reviewCommand),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BoundaryExceptionState.Resolved.ToString(), result.Value.State);
        Assert.Equal(BoundaryExceptionDecision.RejectedOutside.ToString(), result.Value.Decision);
        Assert.Equal(exception.Version, result.Value.Version);
        Assert.Equal(1, fixture.ExceptionRepo.UpdateCallCount);
        Assert.Equal(1, fixture.AuditWriter.UserActionCallCount);
    }

    [Fact]
    public async Task ReviewBoundaryExceptionReturnsNotFoundWhenFarmAccessDenied()
    {
        var fixture = CreateFixture();
        fixture.AccessService.Decision = SystemManagerFarmAccess.Denied("Not authorized for this farm");

        var reviewCommand = new ReviewBoundaryExceptionCommand(
            fixture.Farm.Id,
            Guid.NewGuid(),
            BoundaryExceptionDecision.RejectedOutside,
            CorrectedPosition: null,
            Reason: "Reason",
            Evidence: JsonDocument.Parse("{}"),
            ExpectedVersion: 0);

        var result = await fixture.ReviewHandler.Handle(
            new ReviewBoundaryExceptionRequest(reviewCommand),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Farm.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetBoundaryExceptionsReturnsListForFarm()
    {
        var fixture = CreateFixture();
        var point = _geometryFactory.CreatePoint(new Coordinate(106.10001, 10.05));
        var exception = BoundaryException.Create(
            fixture.TenantId,
            fixture.Farm.Id,
            fixture.ApprovedBoundary.Id,
            BoundaryExceptionSource.BaselineCandidate,
            "viol-001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            point,
            BoundaryExceptionState.NeedsReview,
            1.1m,
            5.0m,
            "1.0",
            Now.AddMinutes(-10));

        fixture.ExceptionRepo.Store[exception.Id] = exception;

        var query = new GetBoundaryExceptionsQuery(fixture.Farm.Id);
        var result = await fixture.GetExceptionsHandler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(exception.Id, result.Value[0].Id);
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

        var approvedBoundary = FarmBoundary.CreateDraft(
            tenantId,
            farm.Id,
            versionNumber: 1,
            boundaryPolygon,
            FarmBoundarySource.SystemManager,
            sourceSurveyRequestId: null,
            submittedBy: actorId,
            createdAt: Now.AddDays(-1));
        approvedBoundary.Approve(actorId, "Initial boundary", Now.AddDays(-1));

        var farmRepo = new FakeFarmRepository { Farm = farm };
        var boundaryRepo = new FakeFarmBoundaryRepository { CurrentApproved = approvedBoundary };
        var zoneRepo = new FakeFarmZoneRepository();
        var exceptionRepo = new FakeBoundaryExceptionRepository();
        var boundaryPolicy = new FarmBoundaryPolicy(boundaryRepo, zoneRepo, farmRepo);
        var unitOfWork = new FakeFarmUnitOfWork();
        var auditWriter = new FakeAuditWriter();
        var execContext = new FakeExecutionContext(tenantId, actorId);
        var accessService = new FakeSystemManagerAccessService(tenantId, farm.Id);
        var timeProvider = new FixedTimeProvider(Now);

        var classifyHandler = new ClassifyAndRecordPointHandler(
            farmRepo,
            boundaryRepo,
            exceptionRepo,
            boundaryPolicy,
            unitOfWork,
            accessService,
            timeProvider);

        var reviewHandler = new ReviewBoundaryExceptionHandler(
            exceptionRepo,
            unitOfWork,
            auditWriter,
            execContext,
            accessService,
            timeProvider);

        var getExceptionsHandler = new GetBoundaryExceptionsHandler(
            exceptionRepo,
            accessService);

        return new TestFixture(
            tenantId,
            actorId,
            farm,
            approvedBoundary,
            classifyHandler,
            reviewHandler,
            getExceptionsHandler,
            exceptionRepo,
            unitOfWork,
            auditWriter,
            accessService);
    }

    private sealed record TestFixture(
        Guid TenantId,
        Guid ActorId,
        Farm Farm,
        FarmBoundary ApprovedBoundary,
        ClassifyAndRecordPointHandler ClassifyHandler,
        ReviewBoundaryExceptionHandler ReviewHandler,
        GetBoundaryExceptionsHandler GetExceptionsHandler,
        FakeBoundaryExceptionRepository ExceptionRepo,
        FakeFarmUnitOfWork UnitOfWork,
        FakeAuditWriter AuditWriter,
        FakeSystemManagerAccessService AccessService);

    private sealed class FakeFarmRepository : IFarmRepository
    {
        public Farm? Farm { get; set; }
        public Task<Farm?> GetByIdAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
            => Task.FromResult(Farm);
        public Task<Farm?> GetByCodeAsync(Guid tenantId, string farmCode, CancellationToken cancellationToken = default)
            => Task.FromResult<Farm?>(null);
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
        public Task<FarmBoundary?> GetCurrentApprovedAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentApproved);
        public Task<FarmBoundary?> GetByIdAsync(Guid tenantId, Guid farmId, Guid boundaryId, CancellationToken cancellationToken = default)
            => Task.FromResult<FarmBoundary?>(null);
        public Task<int> GetNextVersionNumberAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
            => Task.FromResult(2);
        public void Add(FarmBoundary boundary) { }
        public void Update(FarmBoundary boundary) { }
    }

    private sealed class FakeFarmZoneRepository : IFarmZoneRepository
    {
        public Task<bool> ActiveBoundaryOverlapsAsync(Guid tenantId, Guid farmId, Polygon boundary, Guid? excludingZoneId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task<bool> ActiveCodeExistsAsync(Guid tenantId, Guid farmId, string code, Guid? excludingZoneId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task<FarmZone?> GetByIdAsync(Guid tenantId, Guid farmId, Guid zoneId, CancellationToken cancellationToken = default)
            => Task.FromResult<FarmZone?>(null);
        public Task<FarmZone?> GetByCodeAsync(Guid tenantId, Guid farmId, string code, CancellationToken cancellationToken = default)
            => Task.FromResult<FarmZone?>(null);
        public void Add(FarmZone zone) { }
        public void Update(FarmZone zone) { }
    }

    private sealed class FakeBoundaryExceptionRepository : IBoundaryExceptionRepository
    {
        public Dictionary<Guid, BoundaryException> Store { get; } = [];
        public int AddCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }

        public Task<BoundaryException?> GetByIdAsync(Guid tenantId, Guid exceptionId, CancellationToken cancellationToken = default)
        {
            Store.TryGetValue(exceptionId, out var exception);
            return Task.FromResult(exception);
        }

        public Task<bool> HasUnresolvedSourceAsync(Guid farmBoundaryVersionId, BoundaryExceptionSource source, string sourceReferenceId, CancellationToken cancellationToken = default)
        {
            var exists = Store.Values.Any(e =>
                e.FarmBoundaryVersionId == farmBoundaryVersionId &&
                e.Source == source &&
                e.SourceReferenceId == sourceReferenceId &&
                e.State != BoundaryExceptionState.Resolved);
            return Task.FromResult(exists);
        }

        public Task<IReadOnlyList<BoundaryException>> ListByFarmAsync(Guid tenantId, Guid farmId, BoundaryExceptionState? state = null, CancellationToken cancellationToken = default)
        {
            var query = Store.Values.Where(e => e.TenantId == tenantId && e.FarmId == farmId);
            if (state.HasValue)
            {
                query = query.Where(e => e.State == state.Value);
            }
            return Task.FromResult<IReadOnlyList<BoundaryException>>(query.ToList());
        }

        public void Add(BoundaryException boundaryException)
        {
            Store[boundaryException.Id] = boundaryException;
            AddCallCount++;
        }

        public void Update(BoundaryException boundaryException)
        {
            Store[boundaryException.Id] = boundaryException;
            UpdateCallCount++;
        }
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
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
