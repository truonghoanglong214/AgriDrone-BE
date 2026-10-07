using System.Text.Json;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;
using Xunit;
using FarmBoundaryReviewItem = AgriDrone.Modules.Farms.Domain.Boundaries.BoundaryException;

namespace AgriDrone.UnitTests.Domain.Farms;

public sealed class FarmBoundaryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 7, 4, 0, 0, TimeSpan.Zero);

    private static readonly GeometryFactory GeometryFactory =
        new(new PrecisionModel(), 4326);

    [Fact]
    public void DraftCopiesInputGeometryAndCanBeRejectedOnce()
    {
        var polygon = Rectangle(0, 0, 10, 10);
        var boundary = CreateDraft(Guid.NewGuid(), 1, polygon);
        polygon.Coordinates[0].X = 50;

        boundary.Reject(Guid.NewGuid(), "Invalid survey evidence", Now.AddMinutes(1));

        Assert.Equal(FarmBoundaryStatus.Rejected, boundary.Status);
        Assert.Equal(0, boundary.Geometry.Coordinates[0].X);
        Assert.Throws<FarmBoundaryDomainException>(() =>
            boundary.Reject(Guid.NewGuid(), "Review again", Now.AddMinutes(2)));
    }

    [Fact]
    public void ApprovalSupersedesCurrentBoundaryAndKeepsZonesInside()
    {
        var farmId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var current = CreateDraft(farmId, 1, Rectangle(0, 0, 10, 10), tenantId);
        FarmBoundaryApprovalService.Approve(
            current,
            currentApproved: null,
            farmZones: [],
            Guid.NewGuid(),
            "Initial verification",
            Now.AddMinutes(1));

        var replacement = CreateDraft(farmId, 2, Rectangle(0, 0, 20, 20), tenantId);
        var zones = new[]
        {
            CreateZone(farmId, "NORTH", Rectangle(1, 1, 5, 5)),
            CreateZone(farmId, "SOUTH", Rectangle(5, 1, 9, 5))
        };

        FarmBoundaryApprovalService.Approve(
            replacement,
            current,
            zones,
            Guid.NewGuid(),
            "Expanded verified boundary",
            Now.AddMinutes(2));

        Assert.Equal(FarmBoundaryStatus.Superseded, current.Status);
        Assert.Equal(replacement.Id, current.SupersededByBoundaryId);
        Assert.Equal(FarmBoundaryStatus.Approved, replacement.Status);
    }

    [Fact]
    public void ApprovalRejectsOutsideAndOverlappingActiveZones()
    {
        var farmId = Guid.NewGuid();
        var outsideDraft = CreateDraft(farmId, 1, Rectangle(0, 0, 10, 10));
        var outside = CreateZone(farmId, "OUT", Rectangle(9, 9, 11, 11));

        var outsideError = Assert.Throws<FarmBoundaryDomainException>(() =>
            FarmBoundaryApprovalService.Approve(
                outsideDraft,
                null,
                [outside],
                Guid.NewGuid(),
                "Review",
                Now.AddMinutes(1)));
        Assert.Equal(FarmBoundaryDomainErrorCodes.ZoneOutsideBoundary, outsideError.Code);

        var overlapDraft = CreateDraft(farmId, 2, Rectangle(0, 0, 10, 10));
        var overlapError = Assert.Throws<FarmBoundaryDomainException>(() =>
            FarmBoundaryApprovalService.Approve(
                overlapDraft,
                null,
                [
                    CreateZone(farmId, "A", Rectangle(1, 1, 6, 6)),
                    CreateZone(farmId, "B", Rectangle(5, 5, 8, 8))
                ],
                Guid.NewGuid(),
                "Review",
                Now.AddMinutes(1)));
        Assert.Equal(FarmBoundaryDomainErrorCodes.ZonesOverlap, overlapError.Code);
    }

    [Fact]
    public void FailedReplacementValidationDoesNotSupersedeCurrentBoundary()
    {
        var farmId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var current = CreateDraft(farmId, 2, Rectangle(0, 0, 10, 10), tenantId);
        FarmBoundaryApprovalService.Approve(
            current,
            null,
            [],
            Guid.NewGuid(),
            "Current",
            Now.AddMinutes(1));
        var staleDraft = CreateDraft(farmId, 1, Rectangle(0, 0, 10, 10), tenantId);

        Assert.Throws<FarmBoundaryDomainException>(() =>
            FarmBoundaryApprovalService.Approve(
                staleDraft,
                current,
                [],
                Guid.NewGuid(),
                "Stale",
                Now.AddMinutes(2)));

        Assert.Equal(FarmBoundaryStatus.Approved, current.Status);
        Assert.Equal(FarmBoundaryStatus.Draft, staleDraft.Status);
    }

    [Fact]
    public void BoundaryExceptionPreservesOriginalPositionWhenLocationIsCorrected()
    {
        var original = GeometryFactory.CreatePoint(new Coordinate(10, 10));
        var corrected = GeometryFactory.CreatePoint(new Coordinate(9.9, 9.9));
        var reviewItem = CreateReviewItem(original);
        original.X = 100;

        using var evidence = JsonDocument.Parse("""{"mediaId":"evidence-1"}""");
        reviewItem.Resolve(
            BoundaryExceptionDecision.LocationCorrected,
            corrected,
            Guid.NewGuid(),
            Now.AddMinutes(1),
            "GPS correction verified",
            evidence);
        corrected.X = 50;

        Assert.Equal(BoundaryExceptionState.Resolved, reviewItem.State);
        Assert.Equal(10, reviewItem.OriginalPosition.X);
        Assert.Equal(9.9, reviewItem.CorrectedPosition!.X, 6);
        Assert.Equal("evidence-1", reviewItem.ReviewEvidence!.RootElement.GetProperty("mediaId").GetString());
    }

    [Fact]
    public void CorrectedPositionRequiresLocationCorrectedDecision()
    {
        var reviewItem = CreateReviewItem(
            GeometryFactory.CreatePoint(new Coordinate(10, 10)));
        using var evidence = JsonDocument.Parse("{}");

        var error = Assert.Throws<FarmBoundaryDomainException>(() =>
            reviewItem.Resolve(
                BoundaryExceptionDecision.AcceptedInside,
                GeometryFactory.CreatePoint(new Coordinate(9, 9)),
                Guid.NewGuid(),
                Now.AddMinutes(1),
                "Accepted",
                evidence));

        Assert.Equal(FarmBoundaryDomainErrorCodes.CorrectedPositionNotAllowed, error.Code);
        Assert.Equal(BoundaryExceptionState.NeedsReview, reviewItem.State);
    }

    private static FarmBoundary CreateDraft(
        Guid farmId,
        int version,
        Polygon polygon,
        Guid? tenantId = null) =>
        FarmBoundary.CreateDraft(
            tenantId ?? Guid.NewGuid(),
            farmId,
            version,
            polygon,
            FarmBoundarySource.TenantOwner,
            sourceSurveyRequestId: null,
            submittedBy: Guid.NewGuid(),
            Now);

    private static FarmZone CreateZone(Guid farmId, string code, Polygon polygon) =>
        FarmZone.Create(
            farmId,
            code,
            code,
            polygon,
            areaHectares: 1,
            GeneralStatus.Active,
            Guid.NewGuid(),
            Now);

    private static FarmBoundaryReviewItem CreateReviewItem(Point original) =>
        FarmBoundaryReviewItem.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            BoundaryExceptionSource.BaselineCandidate,
            "candidate-1",
            surveyOrderId: null,
            missionId: null,
            original,
            BoundaryExceptionState.NeedsReview,
            measuredDistanceMeters: 0.75m,
            thresholdMeters: 1.5m,
            policyVersion: "boundary-distance-v1",
            Now);

    private static Polygon Rectangle(
        double minX,
        double minY,
        double maxX,
        double maxY) => GeometryFactory.CreatePolygon(
        [
            new Coordinate(minX, minY),
            new Coordinate(maxX, minY),
            new Coordinate(maxX, maxY),
            new Coordinate(minX, maxY),
            new Coordinate(minX, minY)
        ]);
}
