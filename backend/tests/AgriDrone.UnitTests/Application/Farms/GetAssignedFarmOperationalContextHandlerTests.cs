using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Application.Features.GetAssignedFarmOperationalContext;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Farms;

public sealed class GetAssignedFarmOperationalContextHandlerTests
{
    [Fact]
    public async Task HandleReturnsFailureWhenActorNotAuthenticated()
    {
        var fixture = new TestFixture { ActorId = null };

        var result = await fixture.Handler.Handle(
            new GetAssignedFarmOperationalContextQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationError.CurrentUserRequired().Code, result.Error.Code);
    }

    [Fact]
    public async Task HandleReturnsNotFoundWhenAccessDenied()
    {
        var farmId = Guid.NewGuid();
        var fixture = new TestFixture
        {
            ActorId = Guid.NewGuid(),
            AccessResult = SystemManagerFarmAccess.Denied("Not assigned to this farm")
        };

        var result = await fixture.Handler.Handle(
            new GetAssignedFarmOperationalContextQuery(farmId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Farm.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task HandleReturnsNotFoundWhenFarmNotFound()
    {
        var farmId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var fixture = new TestFixture
        {
            ActorId = Guid.NewGuid(),
            AccessResult = SystemManagerFarmAccess.Allowed(tenantId, farmId, Guid.NewGuid()),
            QueryDto = null
        };

        var result = await fixture.Handler.Handle(
            new GetAssignedFarmOperationalContextQuery(farmId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Farm.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task HandleReturnsOperationalContextWhenValid()
    {
        var farmId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var geometryFactory = new GeometryFactory();
        var boundaryPolygon = geometryFactory.CreatePolygon([
            new Coordinate(106.0, 10.0),
            new Coordinate(106.1, 10.0),
            new Coordinate(106.1, 10.1),
            new Coordinate(106.0, 10.1),
            new Coordinate(106.0, 10.0)
        ]);

        var dto = new FarmOperationalContextDto(
            farmId,
            tenantId,
            "FARM-001",
            "Test Farm",
            "123 Farm Road",
            "Active",
            10.5m,
            1,
            new ApprovedBoundaryDto(
                Guid.NewGuid(),
                1,
                boundaryPolygon,
                "SYSTEM_MANAGER",
                "evidence-ref-1",
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "Approved after review",
                1),
            [
                new OperationalZoneDto(
                    Guid.NewGuid(),
                    "ZONE-A",
                    "Zone A",
                    "Active",
                    5.0m,
                    boundaryPolygon,
                    1)
            ],
            new PublishedBaseMapSummaryDto(
                Guid.NewGuid(),
                1,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                Guid.NewGuid(),
                1));

        var fixture = new TestFixture
        {
            ActorId = Guid.NewGuid(),
            AccessResult = SystemManagerFarmAccess.Allowed(tenantId, farmId, Guid.NewGuid()),
            QueryDto = dto
        };

        var result = await fixture.Handler.Handle(
            new GetAssignedFarmOperationalContextQuery(farmId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(farmId, result.Value.FarmId);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal("FARM-001", result.Value.Code);
        Assert.NotNull(result.Value.ApprovedBoundary);
        Assert.Equal(1, result.Value.ApprovedBoundary!.VersionNumber);
        Assert.Single(result.Value.Zones);
        Assert.NotNull(result.Value.PublishedBaseMap);
        Assert.Equal(1, result.Value.PublishedBaseMap!.VersionNumber);
    }

    private sealed class TestFixture
    {
        public Guid? ActorId { get; set; } = Guid.NewGuid();
        public SystemManagerFarmAccess AccessResult { get; set; } = SystemManagerFarmAccess.Denied("Denied");
        public FarmOperationalContextDto? QueryDto { get; set; }

        public GetAssignedFarmOperationalContextQueryHandler Handler =>
            new(
                new FakeOperationalContextQueries(this),
                new FakeSystemManagerAccessService(this),
                new FakeExecutionContext(this));
    }

    private sealed class FakeOperationalContextQueries(TestFixture fixture) : IFarmOperationalContextQueries
    {
        public Task<FarmOperationalContextDto?> GetAssignedFarmOperationalContextAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(fixture.QueryDto);
    }

    private sealed class FakeSystemManagerAccessService(TestFixture fixture) : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(fixture.AccessResult);
    }

    private sealed class FakeExecutionContext(TestFixture fixture) : IExecutionContext
    {
        public Guid? ActorId => fixture.ActorId;
        public Guid? TenantId => null;
        public Guid CorrelationId => Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
        public bool IsInitialized => true;
    }
}
