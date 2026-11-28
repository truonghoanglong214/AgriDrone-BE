using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Features.Processing;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class ComboDiseasePoleLinkingTests
{
    [Fact]
    public void MatchDetectionToPolesWithinThresholdReturnsSnapshotCandidateAndProfileIds()
    {
        var plantId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var candidateRecordId = Guid.NewGuid();
        var sourceCandidateId = Guid.NewGuid();
        var poles = new List<MissionDiseaseJobProcessor.PlantMatchTarget>
        {
            new(
                PlantId: plantId,
                PlantCode: "TV-03-05",
                Latitude: 10.750000,
                Longitude: 106.660000,
                RowIndex: 3,
                ColumnIndex: 5,
                PlantProfileId: profileId,
                MappingCandidateId: candidateRecordId,
                SourceCandidateId: sourceCandidateId)
        };

        // ~0.55m north of pole
        var match = MissionDiseaseJobProcessor.MatchDetectionToPoles(
            10.750005,
            106.660000,
            poles,
            matchingToleranceM: 2.5);

        Assert.True(match.IsWithinThreshold);
        Assert.Equal(plantId, match.MatchedPlantId);
        Assert.Equal(profileId, match.PlantProfileId);
        Assert.Equal(sourceCandidateId, match.MappingCandidateId);
        Assert.Equal(candidateRecordId, match.MappingCandidateRecordId);
        Assert.Equal("TV-03-05", match.PlantCode);
        Assert.Equal(3, match.Row);
        Assert.Equal(5, match.Col);
        Assert.NotNull(match.DistanceM);
        Assert.InRange(match.DistanceM.Value, 0.1, 2.5);
        Assert.NotNull(match.MatchConf);
    }

    [Fact]
    public void MatchDetectionToPolesOutsideThresholdClearsAllPoleIdentityAndKeepsDistance()
    {
        var poles = new List<MissionDiseaseJobProcessor.PlantMatchTarget>
        {
            new(
                PlantId: Guid.NewGuid(),
                PlantCode: "TV-01-01",
                Latitude: 10.750000,
                Longitude: 106.660000,
                RowIndex: 1,
                ColumnIndex: 1,
                PlantProfileId: Guid.NewGuid(),
                MappingCandidateId: Guid.NewGuid(),
                SourceCandidateId: Guid.NewGuid())
        };

        // ~11.1m north of nearest pole (outside 2.5m tolerance)
        var match = MissionDiseaseJobProcessor.MatchDetectionToPoles(
            10.750100,
            106.660000,
            poles,
            matchingToleranceM: 2.5);

        Assert.False(match.IsWithinThreshold);
        Assert.Null(match.MatchedPlantId);
        Assert.Null(match.PlantProfileId);
        Assert.Null(match.MappingCandidateId);
        Assert.Null(match.MappingCandidateRecordId);
        Assert.Null(match.PlantCode);
        Assert.Null(match.Row);
        Assert.Null(match.Col);
        Assert.Null(match.MatchConf);
        Assert.NotNull(match.DistanceM);
        Assert.True(match.DistanceM > 2.5);
    }

    [Fact]
    public void MatchDetectionToPolesWithNoPolesReturnsEmptyOutcome()
    {
        var match = MissionDiseaseJobProcessor.MatchDetectionToPoles(10.75, 106.66, [], 2.5);

        Assert.False(match.IsWithinThreshold);
        Assert.Null(match.MatchedPlantId);
        Assert.Null(match.PlantProfileId);
        Assert.Null(match.MappingCandidateId);
        Assert.Null(match.DistanceM);
    }

    [Fact]
    public void BuildProposedDiseaseZonesPopulatesMappingCandidateIdOnMemberships()
    {
        var orderId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        var findingId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();

        var policy = new ComboInferencePolicy(
            ModelVersionId: modelId,
            Version: "combo-v2",
            Confidence: 0.25,
            ClusterRadiusMeters: 4.5,
            BufferMeters: 1.5,
            MinClusterTrees: 1);

        var zones = MissionDiseaseJobProcessor.BuildProposedDiseaseZones(
            orderId,
            attemptId,
            version: 1,
            policy,
            [
                new MissionDiseaseJobProcessor.ZoneFindingCandidate(
                    FindingId: findingId,
                    ClassName: "LeafSpot",
                    Lat: 10.75,
                    Lon: 106.66,
                    Confidence: 0.89m,
                    PlantProfileId: profileId,
                    MappingCandidateId: candidateId,
                    EvidenceMediaId: mediaId)
            ]);

        var zone = Assert.Single(zones);
        var member = Assert.Single(zone.Memberships);
        Assert.Equal(findingId, member.FindingId);
        Assert.Equal(profileId, member.PlantProfileId);
        Assert.Equal(candidateId, member.MappingCandidateId);
    }

    [Fact]
    public void ResolveFindingIdentityBackfillsCandidateIdFromSuggestedPlantForSavedObservations()
    {
        var plantId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var candidateRecordId = Guid.NewGuid();
        var sourceCandidateId = Guid.NewGuid();

        var pole = new MissionComboAnalysisPublisher.SnapshotPoleRow(
            MappingCandidateId: candidateRecordId,
            SourceCandidateId: sourceCandidateId,
            PlantProfileId: profileId,
            TargetPlantProfileId: null,
            PlantId: plantId);

        var (resolvedProfile, resolvedCandidate) = MissionComboAnalysisPublisher.ResolveFindingIdentity(
            suggestedPlantId: plantId,
            resolvedPlantId: null,
            metadataProfileId: profileId,
            metadataCandidateId: null,
            polesByPlantId: new Dictionary<Guid, MissionComboAnalysisPublisher.SnapshotPoleRow> { [plantId] = pole },
            polesBySourceCandidateId: new Dictionary<Guid, MissionComboAnalysisPublisher.SnapshotPoleRow> { [sourceCandidateId] = pole },
            polesByCandidateRecordId: new Dictionary<Guid, MissionComboAnalysisPublisher.SnapshotPoleRow> { [candidateRecordId] = pole },
            polesByProfileId: new Dictionary<Guid, MissionComboAnalysisPublisher.SnapshotPoleRow> { [profileId] = pole },
            plantIdToProfileMap: new Dictionary<Guid, Guid>());

        Assert.Equal(profileId, resolvedProfile);
        Assert.Equal(sourceCandidateId, resolvedCandidate);
    }

    [Fact]
    public void ResolveFindingIdentityClearsLeakedProfileWhenDetectionWasOutsideThreshold()
    {
        var profileId = Guid.NewGuid();
        var pole = new MissionComboAnalysisPublisher.SnapshotPoleRow(
            MappingCandidateId: Guid.NewGuid(),
            SourceCandidateId: Guid.NewGuid(),
            PlantProfileId: profileId,
            TargetPlantProfileId: null,
            PlantId: Guid.NewGuid());

        // Existing Tam Vu bug: SuggestedPlantId was null (outside threshold), yet metadata leaked plant_profile_id.
        var (resolvedProfile, resolvedCandidate) = MissionComboAnalysisPublisher.ResolveFindingIdentity(
            suggestedPlantId: null,
            resolvedPlantId: null,
            metadataProfileId: profileId,
            metadataCandidateId: null,
            polesByPlantId: new Dictionary<Guid, MissionComboAnalysisPublisher.SnapshotPoleRow>(),
            polesBySourceCandidateId: new Dictionary<Guid, MissionComboAnalysisPublisher.SnapshotPoleRow>(),
            polesByCandidateRecordId: new Dictionary<Guid, MissionComboAnalysisPublisher.SnapshotPoleRow>(),
            polesByProfileId: new Dictionary<Guid, MissionComboAnalysisPublisher.SnapshotPoleRow> { [profileId] = pole },
            plantIdToProfileMap: new Dictionary<Guid, Guid>());

        Assert.Null(resolvedProfile);
        Assert.Null(resolvedCandidate);
    }

    [Fact]
    public void ResolveFindingIdentitySupportsManualResolutionViaPlantProfileLookup()
    {
        var resolvedPlantId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var sourceCandidateId = Guid.NewGuid();
        var pole = new MissionComboAnalysisPublisher.SnapshotPoleRow(
            MappingCandidateId: Guid.NewGuid(),
            SourceCandidateId: sourceCandidateId,
            PlantProfileId: profileId,
            TargetPlantProfileId: null,
            PlantId: null);

        var (resolvedProfile, resolvedCandidate) = MissionComboAnalysisPublisher.ResolveFindingIdentity(
            suggestedPlantId: null,
            resolvedPlantId: resolvedPlantId,
            metadataProfileId: null,
            metadataCandidateId: null,
            polesByPlantId: new Dictionary<Guid, MissionComboAnalysisPublisher.SnapshotPoleRow>(),
            polesBySourceCandidateId: new Dictionary<Guid, MissionComboAnalysisPublisher.SnapshotPoleRow>(),
            polesByCandidateRecordId: new Dictionary<Guid, MissionComboAnalysisPublisher.SnapshotPoleRow>(),
            polesByProfileId: new Dictionary<Guid, MissionComboAnalysisPublisher.SnapshotPoleRow> { [profileId] = pole },
            plantIdToProfileMap: new Dictionary<Guid, Guid> { [resolvedPlantId] = profileId });

        Assert.Equal(profileId, resolvedProfile);
        Assert.Equal(sourceCandidateId, resolvedCandidate);
    }

    [Fact]
    public void TryReadOptionalGuidHandlesJsonNullAndNonStringSafely()
    {
        var expectedProfileId = Guid.NewGuid();
        using var doc = JsonDocument.Parse($$"""
            {
                "plant_profile_id": null,
                "mapping_candidate_id": null,
                "empty_guid": "00000000-0000-0000-0000-000000000000",
                "bad_type": 12345,
                "bad_string": "not-a-guid",
                "valid_profile_id": "{{expectedProfileId:D}}"
            }
            """);

        var root = doc.RootElement;

        Assert.Null(MissionComboAnalysisPublisher.TryReadOptionalGuid(root, "plant_profile_id"));
        Assert.Null(MissionComboAnalysisPublisher.TryReadOptionalGuid(root, "mapping_candidate_id"));
        Assert.Null(MissionComboAnalysisPublisher.TryReadOptionalGuid(root, "missing_key"));
        Assert.Null(MissionComboAnalysisPublisher.TryReadOptionalGuid(root, "empty_guid"));
        Assert.Null(MissionComboAnalysisPublisher.TryReadOptionalGuid(root, "bad_type"));
        Assert.Null(MissionComboAnalysisPublisher.TryReadOptionalGuid(root, "bad_string"));
        Assert.Equal(expectedProfileId, MissionComboAnalysisPublisher.TryReadOptionalGuid(root, "valid_profile_id"));
    }

    [Fact]
    public void BuildProposedDiseaseZonesSkipsClustersSmallerThanMinimumTrees()
    {
        var policy = new ComboInferencePolicy(
            ModelVersionId: Guid.NewGuid(),
            Version: "combo-v2",
            Confidence: 0.25,
            ClusterRadiusMeters: 4.5,
            BufferMeters: 1.5,
            MinClusterTrees: 3);

        var zones = MissionDiseaseJobProcessor.BuildProposedDiseaseZones(
            Guid.NewGuid(),
            Guid.NewGuid(),
            version: 1,
            policy,
            [
                new(Guid.NewGuid(), "BROWN_SPOT", 10.75000, 106.66000, 0.85m, Guid.NewGuid(), Guid.NewGuid(), null),
                new(Guid.NewGuid(), "BROWN_SPOT", 10.75002, 106.66000, 0.82m, Guid.NewGuid(), Guid.NewGuid(), null)
            ]);

        Assert.Empty(zones);
    }

    [Fact]
    public void BuildProposedDiseaseZonesUsesTightConvexHullWithinBoundary()
    {
        var policy = new ComboInferencePolicy(
            ModelVersionId: Guid.NewGuid(),
            Version: "combo-v2",
            Confidence: 0.25,
            ClusterRadiusMeters: 4.5,
            BufferMeters: 1.5,
            MinClusterTrees: 3);

        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var boundary = factory.CreatePolygon(
        [
            new Coordinate(106.65995, 10.74995),
            new Coordinate(106.66020, 10.74995),
            new Coordinate(106.66020, 10.75020),
            new Coordinate(106.65995, 10.75020),
            new Coordinate(106.65995, 10.74995)
        ]);

        // 3 in-boundary poles ~2.8m apart along diagonal + 1 out-of-boundary pole nearby
        var in1 = Guid.NewGuid();
        var in2 = Guid.NewGuid();
        var in3 = Guid.NewGuid();
        var outOfBoundary = Guid.NewGuid();

        var zones = MissionDiseaseJobProcessor.BuildProposedDiseaseZones(
            Guid.NewGuid(),
            Guid.NewGuid(),
            version: 1,
            policy,
            [
                new(in1, "ANTHRACNOSE", 10.75000, 106.66000, 0.90m, Guid.NewGuid(), Guid.NewGuid(), null),
                new(in2, "ANTHRACNOSE", 10.75002, 106.66002, 0.88m, Guid.NewGuid(), Guid.NewGuid(), null),
                new(in3, "ANTHRACNOSE", 10.75004, 106.66004, 0.87m, Guid.NewGuid(), Guid.NewGuid(), null),
                new(outOfBoundary, "ANTHRACNOSE", 10.74993, 106.66000, 0.95m, Guid.NewGuid(), Guid.NewGuid(), null)
            ],
            boundary);

        var zone = Assert.Single(zones);
        Assert.Equal(3, zone.Memberships.Count);
        Assert.DoesNotContain(zone.Memberships, m => m.FindingId == outOfBoundary);

        var ring = zone.Geometry.Coordinates[0];
        var zonePolygon = factory.CreatePolygon(ring.Select(c => new Coordinate(c[0], c[1])).ToArray());
        Assert.True(zonePolygon.IsValid);
        Assert.True(boundary.Covers(zonePolygon));
        Assert.True(ring.Count > 5); // rounded convex hull, not a 5-vertex axis-aligned box
    }
}
