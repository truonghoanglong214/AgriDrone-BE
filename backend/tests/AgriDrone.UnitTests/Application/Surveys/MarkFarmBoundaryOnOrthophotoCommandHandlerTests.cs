using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Orders.MarkFarmBoundaryOnOrthophoto;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class MarkFarmBoundaryOnOrthophotoCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleRejectsUninitializedExecutionContext()
    {
        var fixture = CreateFixture(isInitialized: false);

        var result = await fixture.Handler.Handle(
            CreateCommand(ValidCorners()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("SurveyOrder.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task HandleRejectsWhenCornersIsNull()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            new MarkFarmBoundaryOnOrthophotoCommand(
                fixture.Order.Id,
                null!,
                fixture.Order.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyBoundaryVerificationError.InvalidGeometryCode, result.Error.Code);
        Assert.Contains("four corners are required", result.Error.Description);
    }

    [Fact]
    public async Task HandleRejectsWhenCornerCountIsNotFour()
    {
        var fixture = CreateFixture();
        var threeCorners = new List<CornerPointDto>
        {
            new(1, 106.0, 10.0),
            new(2, 106.01, 10.0),
            new(3, 106.01, 10.01)
        };

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture.Order.Id, threeCorners, fixture.Order.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyBoundaryVerificationError.InvalidGeometryCode, result.Error.Code);
    }

    [Theory]
    [InlineData(1, 2, 2, 4)]
    [InlineData(0, 1, 2, 3)]
    [InlineData(1, 2, 3, 5)]
    public async Task HandleRejectsWhenSequenceIsNotOneToFour(int s1, int s2, int s3, int s4)
    {
        var fixture = CreateFixture();
        var corners = new List<CornerPointDto>
        {
            new(s1, 106.0, 10.0),
            new(s2, 106.01, 10.0),
            new(s3, 106.01, 10.01),
            new(s4, 106.0, 10.01)
        };

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture.Order.Id, corners, fixture.Order.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyBoundaryVerificationError.InvalidGeometryCode, result.Error.Code);
        Assert.Contains("sequences exactly 1, 2, 3, and 4", result.Error.Description);
    }

    [Theory]
    [InlineData(double.NaN, 10.0)]
    [InlineData(106.0, double.NaN)]
    [InlineData(double.PositiveInfinity, 10.0)]
    [InlineData(106.0, double.NegativeInfinity)]
    public async Task HandleRejectsWhenCoordinatesAreNotFinite(double lon, double lat)
    {
        var fixture = CreateFixture();
        var corners = new List<CornerPointDto>
        {
            new(1, lon, lat),
            new(2, 106.01, 10.0),
            new(3, 106.01, 10.01),
            new(4, 106.0, 10.01)
        };

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture.Order.Id, corners, fixture.Order.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyBoundaryVerificationError.InvalidGeometryCode, result.Error.Code);
    }

    [Theory]
    [InlineData(-180.1, 10.0)]
    [InlineData(180.1, 10.0)]
    [InlineData(106.0, -90.1)]
    [InlineData(106.0, 90.1)]
    public async Task HandleRejectsWhenCoordinatesAreOutOfRange(double lon, double lat)
    {
        var fixture = CreateFixture();
        var corners = new List<CornerPointDto>
        {
            new(1, lon, lat),
            new(2, 106.01, 10.0),
            new(3, 106.01, 10.01),
            new(4, 106.0, 10.01)
        };

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture.Order.Id, corners, fixture.Order.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyBoundaryVerificationError.InvalidGeometryCode, result.Error.Code);
    }

    [Fact]
    public async Task HandleRejectsWhenCornersAreNotDistinct()
    {
        var fixture = CreateFixture();
        var corners = new List<CornerPointDto>
        {
            new(1, 106.0, 10.0),
            new(2, 106.0, 10.0), // Duplicate point
            new(3, 106.01, 10.01),
            new(4, 106.0, 10.01)
        };

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture.Order.Id, corners, fixture.Order.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyBoundaryVerificationError.InvalidGeometryCode, result.Error.Code);
        Assert.Contains("distinct points", result.Error.Description);
    }

    [Fact]
    public async Task HandleRejectsWhenPolygonIsSelfIntersecting()
    {
        var fixture = CreateFixture();
        // Self-intersecting bowtie polygon
        var corners = new List<CornerPointDto>
        {
            new(1, 106.0, 10.0),
            new(2, 106.01, 10.01),
            new(3, 106.01, 10.0),
            new(4, 106.0, 10.01)
        };

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture.Order.Id, corners, fixture.Order.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyBoundaryVerificationError.InvalidGeometryCode, result.Error.Code);
    }

    [Fact]
    public async Task HandleRejectsWhenAreaIsBelowMinimumThreshold()
    {
        var fixture = CreateFixture();
        // Area ~ 1.2 m² (< 0.01 ha = 100 m²)
        var corners = new List<CornerPointDto>
        {
            new(1, 106.0, 10.0),
            new(2, 106.00001, 10.0),
            new(3, 106.00001, 10.00001),
            new(4, 106.0, 10.00001)
        };

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture.Order.Id, corners, fixture.Order.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyBoundaryVerificationError.InvalidGeometryCode, result.Error.Code);
        Assert.Contains("below the minimum allowed threshold", result.Error.Description);
    }

    [Fact]
    public async Task HandleRejectsWhenAreaExceedsMaximumThreshold()
    {
        var fixture = CreateFixture();
        // Area ~ 1,200,000 ha (> 5,000 ha)
        var corners = new List<CornerPointDto>
        {
            new(1, 105.0, 10.0),
            new(2, 106.0, 10.0),
            new(3, 106.0, 11.0),
            new(4, 105.0, 11.0)
        };

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture.Order.Id, corners, fixture.Order.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(SurveyBoundaryVerificationError.InvalidGeometryCode, result.Error.Code);
        Assert.Contains("exceeds the maximum allowed threshold", result.Error.Description);
    }

    [Fact]
    public async Task HandleRejectsWhenOrderNotInAllowedState()
    {
        var fixture = CreateFixture();
        // Order in BaselineReady cannot mark boundary on orthophoto
        fixture.Order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        fixture.Order.RecordBaselineDepositConfirmed(Guid.NewGuid(), 500_000m, Now.AddMinutes(5));

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture.Order.Id, ValidCorners(), fixture.Order.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("SurveyOrder.InvalidTransition", result.Error.Code);
    }

    [Fact]
    public async Task HandleSucceedsWhenValidAndOrderInAwaitingBaselineReview()
    {
        var fixture = CreateFixture();
        fixture.Order.MarkBaselineAppointmentConfirmed(Now.AddMinutes(1));
        fixture.Order.RecordBaselineDepositConfirmed(Guid.NewGuid(), 500_000m, Now.AddMinutes(5));
        fixture.Order.StartBaselineMapping(Now.AddHours(1));
        fixture.Order.MarkBaselineCandidatesReady(Now.AddHours(2));

        Assert.Equal(SurveyOrderStatus.AwaitingBaselineReview, fixture.Order.Status);
        fixture.TimeProvider.UtcNow = Now.AddHours(3);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture.Order.Id, ValidCorners(), fixture.Order.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.Order.Id, result.Value.SurveyOrderId);
        Assert.Equal(fixture.BoundarySnapshot.FarmBoundaryId, result.Value.FarmBoundaryId);
        Assert.Equal(1, fixture.UnitOfWork.FlushCallCount);
        Assert.Equal(1, fixture.AuditWriter.UserActionCallCount);
        Assert.Equal(fixture.BoundarySnapshot.FarmBoundaryId, fixture.Order.FarmBoundaryVersionId);
    }

    [Fact]
    public async Task HandleSucceedsWhenValidAndOrderInPendingBoundaryVerification()
    {
        var fixture = CreateFixture(requiresBaselineMapping: false);
        Assert.Equal(SurveyOrderStatus.PendingBoundaryVerification, fixture.Order.Status);

        var result = await fixture.Handler.Handle(
            CreateCommand(fixture.Order.Id, ValidCorners(), fixture.Order.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.Order.Id, result.Value.SurveyOrderId);
        Assert.Equal(fixture.BoundarySnapshot.FarmBoundaryId, result.Value.FarmBoundaryId);
    }

    private static List<CornerPointDto> ValidCorners() =>
    [
        new(1, 106.0, 10.0),
        new(2, 106.01, 10.0),
        new(3, 106.01, 10.01),
        new(4, 106.0, 10.01)
    ];

    private static MarkFarmBoundaryOnOrthophotoCommand CreateCommand(IReadOnlyList<CornerPointDto> corners) =>
        new(Guid.NewGuid(), corners, ExpectedOrderVersion: 1);

    private static MarkFarmBoundaryOnOrthophotoCommand CreateCommand(
        Guid surveyOrderId,
        IReadOnlyList<CornerPointDto> corners,
        uint expectedOrderVersion = 1) =>
        new(surveyOrderId, corners, expectedOrderVersion);

    private static Fixture CreateFixture(bool requiresBaselineMapping = true, bool isInitialized = true)
    {
        var actorId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var order = SurveyOrder.Create(
            $"SO-{Guid.NewGuid():N}"[..20],
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            requiresBaselineMapping,
            previousCompatibleOrderId: null,
            previousOrderCompatibilityPolicyVersion: "test.v1",
            previousOrderCompatibilityReason: "NO_PREVIOUS_PUBLISHED_ORDER",
            createdAt: Now);

        var boundarySnapshot = new SurveyBoundaryApprovalSnapshot(Guid.NewGuid(), 1);
        var unitOfWork = new FakeSurveyBoundaryVerificationUnitOfWork();
        var boundaryRepository = new FakeSurveyOrderBoundaryRepository(order, boundarySnapshot);
        var auditWriter = new FakeAuditWriter();
        var executionContext = new FakeExecutionContext(isInitialized, actorId, correlationId);
        var timeProvider = new FixedTimeProvider(Now);

        var handler = new MarkFarmBoundaryOnOrthophotoCommandHandler(
            unitOfWork,
            boundaryRepository,
            auditWriter,
            executionContext,
            timeProvider);

        return new Fixture(
            handler,
            order,
            boundarySnapshot,
            unitOfWork,
            boundaryRepository,
            auditWriter,
            timeProvider);
    }

    private sealed record Fixture(
        MarkFarmBoundaryOnOrthophotoCommandHandler Handler,
        SurveyOrder Order,
        SurveyBoundaryApprovalSnapshot BoundarySnapshot,
        FakeSurveyBoundaryVerificationUnitOfWork UnitOfWork,
        FakeSurveyOrderBoundaryRepository BoundaryRepository,
        FakeAuditWriter AuditWriter,
        FixedTimeProvider TimeProvider);

    private sealed class FakeSurveyBoundaryVerificationUnitOfWork
        : ISurveyBoundaryVerificationUnitOfWork
    {
        public int FlushCallCount { get; private set; }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            FlushCallCount++;
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public void AddAuditLog(AuditLog auditLog)
        {
        }
    }

    private sealed class FakeSurveyOrderBoundaryRepository(
        SurveyOrder order,
        SurveyBoundaryApprovalSnapshot snapshot)
        : ISurveyOrderBoundaryRepository
    {
        public Task<Result<SurveyOrderBoundaryVerificationState>> GetForVerificationAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            DateTimeOffset verifiedAt,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success(new SurveyOrderBoundaryVerificationState(
                order,
                Guid.NewGuid(),
                Guid.NewGuid(),
                CurrentConfirmedPoleCount: 100,
                ActiveZones: [])));
        }

        public Task<Result<SurveyBoundaryApprovalSnapshot>> StageApprovedBoundaryAsync(
            SurveyOrder order,
            Polygon geometry,
            int? expectedCurrentBoundaryVersionNumber,
            Guid actorUserId,
            string reason,
            string? evidenceReference,
            DateTimeOffset verifiedAt,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success(snapshot));
        }
    }

    private sealed class FakeExecutionContext(
        bool isInitialized,
        Guid actorId,
        Guid correlationId)
        : IExecutionContext
    {
        public bool IsInitialized => isInitialized;
        public Guid? ActorId => isInitialized ? actorId : null;
        public Guid CorrelationId => correlationId;
        public Guid? TenantId => Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
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
            DateTimeOffset createdAt)
        {
            UserActionCallCount++;
        }

        public void AddSystemAdminAction(
            IAuditLogSink sink,
            Guid actorId,
            Guid correlationId,
            string entityType,
            Guid entityId,
            string action,
            JsonDocument? oldData,
            JsonDocument? newData,
            DateTimeOffset createdAt)
        {
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
