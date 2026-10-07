using System.Text.Json;
using AgriDrone.Modules.Plants.Domain.DiseaseZones;
using AgriDrone.Modules.Plants.Domain.Recommendations;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Plants;

public sealed class DiseaseZoneAndRecommendationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 7, 6, 0, 0, TimeSpan.Zero);

    private static readonly GeometryFactory GeometryFactory =
        new(new PrecisionModel(), 4326);

    [Fact]
    public void ProposalPreservesOriginalGeometryAndMembership()
    {
        var geometry = Rectangle(1, 1, 5, 5);
        var plantId = Guid.NewGuid();
        using var candidates = JsonDocument.Parse("[]");
        using var evidence = JsonDocument.Parse("""{"job":"ai-1"}""");
        var zone = DiseaseZone.CreateProposal(
            CreateProposal(geometry, [new(plantId, 0.91m)], candidates, evidence));
        geometry.Coordinates[0].X = 50;

        Assert.Equal(DiseaseZoneStatus.Proposed, zone.Status);
        Assert.Equal(0 + 1, zone.CurrentMembershipVersion);
        Assert.Equal(1, zone.ProposedGeometry.Coordinates[0].X);
        var membership = Assert.Single(zone.Memberships);
        Assert.Equal(plantId, membership.PlantId);
        Assert.Equal(DiseaseZoneMembershipKind.Proposed, membership.Kind);
    }

    [Fact]
    public void ReviewKeepsProposalAndAddsReviewedSnapshotWithApplicableRecommendation()
    {
        var conditionId = Guid.NewGuid();
        var healthLevelId = Guid.NewGuid();
        var proposedPlantId = Guid.NewGuid();
        var reviewedPlantId = Guid.NewGuid();
        var zone = CreateZone(conditionId, healthLevelId, proposedPlantId);
        var recommendation = CreatePublishedRecommendation(conditionId, healthLevelId);
        var reviewedGeometry = Rectangle(2, 2, 6, 6);
        using var reviewEvidence = JsonDocument.Parse("""{"review":"field-check"}""");

        zone.Review(
            reviewedGeometry,
            [new(reviewedPlantId, null)],
            recommendation,
            rejectAllRecommendations: false,
            Guid.NewGuid(),
            Now.AddMinutes(2),
            "Corrected using field evidence",
            reviewEvidence);
        reviewedGeometry.Coordinates[0].X = 70;

        Assert.Equal(DiseaseZoneStatus.Reviewed, zone.Status);
        Assert.Equal(2, zone.CurrentMembershipVersion);
        Assert.Equal(2, zone.Memberships.Count);
        Assert.Equal(1, zone.ProposedGeometry.Coordinates[0].X);
        Assert.Equal(2, zone.ReviewedGeometry!.Coordinates[0].X);
        Assert.Equal(recommendation.Id, zone.SelectedTreatmentRecommendationId);
        Assert.False(zone.RecommendationsRejected);
    }

    [Fact]
    public void ReviewRequiresExactlyOneRecommendationDecision()
    {
        var zone = CreateZone(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        using var evidence = JsonDocument.Parse("{}");

        var exception = Assert.Throws<DiseaseZoneDomainException>(() =>
            zone.Review(
                Rectangle(1, 1, 5, 5),
                [new(Guid.NewGuid(), null)],
                selectedRecommendation: null,
                rejectAllRecommendations: false,
                Guid.NewGuid(),
                Now.AddMinutes(1),
                "No decision",
                evidence));

        Assert.Equal(DiseaseZoneDomainErrorCodes.RecommendationDecisionRequired, exception.Code);
        Assert.Equal(DiseaseZoneStatus.Proposed, zone.Status);
    }

    [Fact]
    public void ReviewRejectsRecommendationForDifferentSeverityWithoutMutation()
    {
        var conditionId = Guid.NewGuid();
        var zone = CreateZone(conditionId, Guid.NewGuid(), Guid.NewGuid());
        var recommendation = CreatePublishedRecommendation(conditionId, Guid.NewGuid());
        using var evidence = JsonDocument.Parse("{}");

        var exception = Assert.Throws<DiseaseZoneDomainException>(() =>
            zone.Review(
                Rectangle(1, 1, 5, 5),
                [new(Guid.NewGuid(), null)],
                recommendation,
                rejectAllRecommendations: false,
                Guid.NewGuid(),
                Now.AddMinutes(2),
                "Wrong severity",
                evidence));

        Assert.Equal(DiseaseZoneDomainErrorCodes.RecommendationNotApplicable, exception.Code);
        Assert.Equal(DiseaseZoneStatus.Proposed, zone.Status);
        Assert.Single(zone.Memberships);
    }

    [Fact]
    public void ReviewedZoneCanRejectAllCandidatesAndPublish()
    {
        var zone = CreateZone(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        using var evidence = JsonDocument.Parse("{}");

        zone.Review(
            Rectangle(1, 1, 5, 5),
            [new(Guid.NewGuid(), null)],
            selectedRecommendation: null,
            rejectAllRecommendations: true,
            Guid.NewGuid(),
            Now.AddMinutes(1),
            "No catalogue recommendation is safe for this zone",
            evidence);
        DiseaseZonePublicationService.Publish(
            zone,
            currentPublished: null,
            Guid.NewGuid(),
            Now.AddMinutes(2));

        Assert.Equal(DiseaseZoneStatus.Published, zone.Status);
        Assert.True(zone.RecommendationsRejected);
        Assert.Null(zone.SelectedTreatmentRecommendationId);
    }

    [Fact]
    public void RecommendationVersionSupersedesPublishedHistory()
    {
        var conditionId = Guid.NewGuid();
        var healthLevelId = Guid.NewGuid();
        var current = CreatePublishedRecommendation(conditionId, healthLevelId);
        var replacement = CreateRecommendation(
            conditionId,
            healthLevelId,
            versionNumber: 2,
            current.Id);

        TreatmentRecommendationPublicationService.Publish(
            replacement,
            current,
            Guid.NewGuid(),
            Now.AddMinutes(3));

        Assert.Equal(TreatmentRecommendationStatus.Superseded, current.Status);
        Assert.Equal(replacement.Id, current.SupersededByRecommendationId);
        Assert.Equal(TreatmentRecommendationStatus.Published, replacement.Status);
    }

    [Fact]
    public void DuplicateMembershipIsRejected()
    {
        var plantId = Guid.NewGuid();
        using var candidates = JsonDocument.Parse("[]");
        using var evidence = JsonDocument.Parse("{}");

        var exception = Assert.Throws<DiseaseZoneDomainException>(() =>
            DiseaseZone.CreateProposal(
                CreateProposal(
                    Rectangle(1, 1, 5, 5),
                    [new(plantId, null), new(plantId, 0.8m)],
                    candidates,
                    evidence)));

        Assert.Equal(DiseaseZoneDomainErrorCodes.DuplicateMembership, exception.Code);
    }

    private static DiseaseZone CreateZone(
        Guid conditionId,
        Guid healthLevelId,
        Guid plantId)
    {
        using var candidates = JsonDocument.Parse("[]");
        using var evidence = JsonDocument.Parse("{}");
        return DiseaseZone.CreateProposal(
            CreateProposal(
                Rectangle(1, 1, 5, 5),
                [new(plantId, 0.9m)],
                candidates,
                evidence,
                conditionId,
                healthLevelId));
    }

    private static DiseaseZoneProposal CreateProposal(
        Polygon geometry,
        IReadOnlyCollection<DiseaseZoneMembershipCandidate> membership,
        JsonDocument candidates,
        JsonDocument evidence,
        Guid? conditionId = null,
        Guid? healthLevelId = null) =>
        new()
        {
            TenantId = Guid.NewGuid(),
            FarmId = Guid.NewGuid(),
            SurveyOrderId = Guid.NewGuid(),
            SurveyResultId = Guid.NewGuid(),
            FarmBoundaryVersionId = Guid.NewGuid(),
            FarmBaseMapVersionId = Guid.NewGuid(),
            PlantConditionId = conditionId ?? Guid.NewGuid(),
            HealthLevelId = healthLevelId ?? Guid.NewGuid(),
            SourceHandoffId = Guid.NewGuid(),
            SourceJobId = Guid.NewGuid(),
            SourceProposalId = $"proposal-{Guid.NewGuid():N}",
            Geometry = geometry,
            Membership = membership,
            RecommendationCandidates = candidates,
            Evidence = evidence,
            ProposedAt = Now
        };

    private static TreatmentRecommendation CreatePublishedRecommendation(
        Guid conditionId,
        Guid healthLevelId)
    {
        var recommendation = CreateRecommendation(
            conditionId,
            healthLevelId,
            versionNumber: 1,
            supersedesId: null);
        recommendation.Publish(Guid.NewGuid(), Now.AddMinutes(1));
        return recommendation;
    }

    private static TreatmentRecommendation CreateRecommendation(
        Guid conditionId,
        Guid healthLevelId,
        int versionNumber,
        Guid? supersedesId) =>
        TreatmentRecommendation.CreateDraft(
            "REC-FUNGAL-MODERATE",
            versionNumber,
            conditionId,
            healthLevelId,
            "Monitor and isolate affected plants",
            "Follow the validated farm protocol and reassess after treatment.",
            "Advisory only; apply according to local regulations and product labels.",
            "Agronomy Board",
            "AGR-2026-10",
            Now,
            Now.AddYears(1),
            supersedesId,
            Guid.NewGuid(),
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
