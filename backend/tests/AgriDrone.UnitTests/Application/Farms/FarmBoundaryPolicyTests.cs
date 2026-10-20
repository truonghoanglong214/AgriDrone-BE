using AgriDrone.Modules.Farms.Application.Abstractions.Boundaries;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.Farms.Infrastructure.Boundaries;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Farms;

public sealed class FarmBoundaryPolicyTests
{
    private readonly GeometryFactory _geometryFactory = new(new PrecisionModel(), 4326);

    [Fact]
    public void ClassifyPointReturnsWithinBoundaryWhenPointIsSafelyInside()
    {
        var policy = CreatePolicy();
        var farmBoundary = CreatePolygon([
            new Coordinate(106.0, 10.0),
            new Coordinate(106.1, 10.0),
            new Coordinate(106.1, 10.1),
            new Coordinate(106.0, 10.1),
            new Coordinate(106.0, 10.0)
        ]);
        var insidePoint = _geometryFactory.CreatePoint(new Coordinate(106.05, 10.05));

        var classification = policy.ClassifyPoint(insidePoint, farmBoundary, nearBoundaryThresholdMeters: 5.0);

        Assert.Equal(PointBoundaryClassification.WithinBoundary, classification);
    }

    [Fact]
    public void ClassifyPointReturnsNearBoundaryWhenPointIsWithinThresholdDistance()
    {
        var policy = CreatePolicy();
        var farmBoundary = CreatePolygon([
            new Coordinate(106.0, 10.0),
            new Coordinate(106.1, 10.0),
            new Coordinate(106.1, 10.1),
            new Coordinate(106.0, 10.1),
            new Coordinate(106.0, 10.0)
        ]);
        // Point just 0.00001 degrees inside or outside the edge (~1.1 meters)
        var nearPoint = _geometryFactory.CreatePoint(new Coordinate(106.00001, 10.05));

        var classification = policy.ClassifyPoint(nearPoint, farmBoundary, nearBoundaryThresholdMeters: 5.0);

        Assert.Equal(PointBoundaryClassification.NearBoundary, classification);
    }

    [Fact]
    public void ClassifyPointReturnsOutOfBoundaryWhenPointIsOutsideAndFartherThanThreshold()
    {
        var policy = CreatePolicy();
        var farmBoundary = CreatePolygon([
            new Coordinate(106.0, 10.0),
            new Coordinate(106.1, 10.0),
            new Coordinate(106.1, 10.1),
            new Coordinate(106.0, 10.1),
            new Coordinate(106.0, 10.0)
        ]);
        var outsidePoint = _geometryFactory.CreatePoint(new Coordinate(106.2, 10.2));

        var classification = policy.ClassifyPoint(outsidePoint, farmBoundary, nearBoundaryThresholdMeters: 5.0);

        Assert.Equal(PointBoundaryClassification.OutOfBoundary, classification);
    }

    [Fact]
    public void ValidateZoneWithinBoundaryReturnsSuccessWhenZoneIsCompletelyInsideFarmBoundary()
    {
        var policy = CreatePolicy();
        var farmBoundary = CreatePolygon([
            new Coordinate(106.0, 10.0),
            new Coordinate(106.1, 10.0),
            new Coordinate(106.1, 10.1),
            new Coordinate(106.0, 10.1),
            new Coordinate(106.0, 10.0)
        ]);
        var zoneBoundary = CreatePolygon([
            new Coordinate(106.02, 10.02),
            new Coordinate(106.08, 10.02),
            new Coordinate(106.08, 10.08),
            new Coordinate(106.02, 10.08),
            new Coordinate(106.02, 10.02)
        ]);

        var result = policy.ValidateZoneWithinBoundary(zoneBoundary, farmBoundary);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateZoneWithinBoundaryReturnsFailureWhenZoneExtendsOutsideFarmBoundary()
    {
        var policy = CreatePolicy();
        var farmBoundary = CreatePolygon([
            new Coordinate(106.0, 10.0),
            new Coordinate(106.1, 10.0),
            new Coordinate(106.1, 10.1),
            new Coordinate(106.0, 10.1),
            new Coordinate(106.0, 10.0)
        ]);
        var outsideZone = CreatePolygon([
            new Coordinate(106.05, 10.05),
            new Coordinate(106.15, 10.05),
            new Coordinate(106.15, 10.15),
            new Coordinate(106.05, 10.15),
            new Coordinate(106.05, 10.05)
        ]);

        var result = policy.ValidateZoneWithinBoundary(outsideZone, farmBoundary);

        Assert.True(result.IsFailure);
        Assert.Equal(FarmZoneError.BoundaryOutsideFarm().Code, result.Error.Code);
    }

    [Fact]
    public async Task ValidateZoneAsyncReturnsUnmappedRequiresCaptureSessionWhenNoApprovedBoundaryExists()
    {
        var boundaryRepo = new FakeFarmBoundaryRepository();
        var zoneRepo = new FakeFarmZoneRepository();
        var policy = new FarmBoundaryPolicy(boundaryRepo, zoneRepo);
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var zoneBoundary = CreatePolygon([
            new Coordinate(106.02, 10.02),
            new Coordinate(106.08, 10.02),
            new Coordinate(106.08, 10.08),
            new Coordinate(106.02, 10.08),
            new Coordinate(106.02, 10.02)
        ]);

        var result = await policy.ValidateZoneAsync(
            tenantId,
            farmId,
            zoneBoundary,
            zoneAreaHectares: 1.0m,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(FarmError.UnmappedFarmBoundaryRequiresCaptureSession().Code, result.Error.Code);
    }

    [Fact]
    public async Task ValidateZoneAsyncReturnsFailureWhenZoneOverlapsExistingZone()
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var farmBoundary = CreatePolygon([
            new Coordinate(106.0, 10.0),
            new Coordinate(106.1, 10.0),
            new Coordinate(106.1, 10.1),
            new Coordinate(106.0, 10.1),
            new Coordinate(106.0, 10.0)
        ]);
        var boundary = FarmBoundary.CreateDraft(
            tenantId,
            farmId,
            versionNumber: 1,
            farmBoundary,
            FarmBoundarySource.SystemManager,
            sourceSurveyRequestId: null,
            submittedBy: Guid.NewGuid(),
            createdAt: DateTimeOffset.UtcNow);
        boundary.Approve(Guid.NewGuid(), "Approved", DateTimeOffset.UtcNow);

        var boundaryRepo = new FakeFarmBoundaryRepository { CurrentApproved = boundary };
        var zoneRepo = new FakeFarmZoneRepository { Overlaps = true };
        var policy = new FarmBoundaryPolicy(boundaryRepo, zoneRepo);

        var zoneBoundary = CreatePolygon([
            new Coordinate(106.02, 10.02),
            new Coordinate(106.08, 10.02),
            new Coordinate(106.08, 10.08),
            new Coordinate(106.02, 10.08),
            new Coordinate(106.02, 10.02)
        ]);

        var result = await policy.ValidateZoneAsync(
            tenantId,
            farmId,
            zoneBoundary,
            zoneAreaHectares: 1.0m,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(FarmZoneError.BoundaryOverlaps().Code, result.Error.Code);
    }

    [Fact]
    public void CalculateDistanceToBoundaryMetersReturnsAccurateGeodesicDistanceForHorizontalEdge()
    {
        var policy = CreatePolicy();
        var farmBoundary = CreatePolygon([
            new Coordinate(106.0, 10.0),
            new Coordinate(106.1, 10.0),
            new Coordinate(106.1, 10.1),
            new Coordinate(106.0, 10.1),
            new Coordinate(106.0, 10.0)
        ]);
        // 0.0001 degrees North of the top edge at latitude 10.1
        var point = _geometryFactory.CreatePoint(new Coordinate(106.05, 10.1001));

        var distance = policy.CalculateDistanceToBoundaryMeters(point, farmBoundary);

        // Expected distance: 0.0001 * 111,320 = 11.132 meters
        Assert.InRange(distance, 11.12, 11.14);
    }

    [Fact]
    public void CalculateDistanceToBoundaryMetersReturnsAccurateGeodesicDistanceForVerticalEdge()
    {
        var policy = CreatePolicy();
        var farmBoundary = CreatePolygon([
            new Coordinate(106.0, 10.0),
            new Coordinate(106.1, 10.0),
            new Coordinate(106.1, 10.1),
            new Coordinate(106.0, 10.1),
            new Coordinate(106.0, 10.0)
        ]);
        // 0.0001 degrees East of the right edge at longitude 106.1, latitude 10.05
        var point = _geometryFactory.CreatePoint(new Coordinate(106.1001, 10.05));

        var distance = policy.CalculateDistanceToBoundaryMeters(point, farmBoundary);

        // Expected distance: 0.0001 * 111,320 * cos(10.05°) ≈ 10.96 meters
        Assert.InRange(distance, 10.95, 10.97);
    }

    [Fact]
    public void CalculateDistanceToBoundaryMetersReturnsZeroWhenPointIsOnEdge()
    {
        var policy = CreatePolicy();
        var farmBoundary = CreatePolygon([
            new Coordinate(106.0, 10.0),
            new Coordinate(106.1, 10.0),
            new Coordinate(106.1, 10.1),
            new Coordinate(106.0, 10.1),
            new Coordinate(106.0, 10.0)
        ]);
        var pointOnEdge = _geometryFactory.CreatePoint(new Coordinate(106.05, 10.0));

        var distance = policy.CalculateDistanceToBoundaryMeters(pointOnEdge, farmBoundary);

        Assert.Equal(0.0, distance, precision: 4);
    }

    private static FarmBoundaryPolicy CreatePolicy()
    {
        return new FarmBoundaryPolicy(new FakeFarmBoundaryRepository(), new FakeFarmZoneRepository());
    }

    private Polygon CreatePolygon(Coordinate[] coordinates)
    {
        var linearRing = _geometryFactory.CreateLinearRing(coordinates);
        var polygon = _geometryFactory.CreatePolygon(linearRing);
        polygon.SRID = 4326;
        return polygon;
    }

    private sealed class FakeFarmBoundaryRepository : IFarmBoundaryRepository
    {
        public FarmBoundary? CurrentApproved { get; set; }

        public Task<FarmBoundary?> GetCurrentApprovedAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentApproved);

        public Task<FarmBoundary?> GetByIdAsync(Guid tenantId, Guid farmId, Guid boundaryId, CancellationToken cancellationToken = default)
            => Task.FromResult<FarmBoundary?>(null);

        public Task<int> GetNextVersionNumberAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public void Add(FarmBoundary boundary) { }
        public void Update(FarmBoundary boundary) { }
    }

    private sealed class FakeFarmZoneRepository : IFarmZoneRepository
    {
        public bool Overlaps { get; set; }

        public Task<bool> ActiveBoundaryOverlapsAsync(
            Guid tenantId,
            Guid farmId,
            Polygon boundary,
            Guid? excludingZoneId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Overlaps);

                public List<FarmZone> ActiveZones { get; } = [];
        public Task<IReadOnlyList<FarmZone>> GetActiveZonesByFarmIdAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FarmZone>>(ActiveZones);

        public Task<bool> ActiveCodeExistsAsync(
            Guid tenantId,
            Guid farmId,
            string code,
            Guid? excludingZoneId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<FarmZone?> GetByIdAsync(Guid tenantId, Guid farmId, Guid zoneId, CancellationToken cancellationToken = default)
            => Task.FromResult<FarmZone?>(null);

        public Task<FarmZone?> GetByCodeAsync(Guid tenantId, Guid farmId, string code, CancellationToken cancellationToken = default)
            => Task.FromResult<FarmZone?>(null);

        public void Add(FarmZone zone) { }
        public void Update(FarmZone zone) { }
    }
}
