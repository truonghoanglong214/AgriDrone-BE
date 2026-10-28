using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Integration;

public sealed class StreamlinedBaselineMappingCandidatesReadyV3Tests
{
    private static readonly Guid SurveyOrderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MissionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FarmId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ZoneId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void ValidatorAcceptsStreamlinedLeanPayloadFromBe2()
    {
        // Arrange - Minimal lean contract from BE2 mission_plant_gps:
        // No HandoffId, no FarmBoundaryVersionId, no CausationId, no EvidenceMediaAssetIds,
        // no BoundaryClassification, only (Lat, Lon, Row, Col, Conf, TreeId).
        var leanPayload = new BaselineMappingCandidatesReadyV3(
            SurveyOrderId: SurveyOrderId,
            MissionId: MissionId,
            FarmId: FarmId,
            Zones:
            [
                new ZoneMappingCandidatesV3(
                    ZoneId: ZoneId,
                    GridBearingDeg: 0.0,
                    RowSpacingM: 3.5,
                    PlantSpacingM: 2.0,
                    Candidates:
                    [
                        new MappingCandidateV3(
                            Longitude: 106.123456,
                            Latitude: 10.654321,
                            ProposedRowIndex: 1,
                            ProposedColumnIndex: 5,
                            PositionConfidence: 0.98m,
                            TreeId: "tree_r1_c5")
                    ])
            ]);

        // Act
        var errors = V3BusinessContractValidators.Validate(leanPayload);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidatorRejectsWhenBothCoordinateFormatsAreMissing()
    {
        // Arrange - candidate with neither OriginalPosition nor Longitude/Latitude (0,0 with null OriginalPosition is rejected by validation)
        var payloadWithoutCoords = new BaselineMappingCandidatesReadyV3(
            SurveyOrderId: SurveyOrderId,
            MissionId: MissionId,
            FarmId: FarmId,
            Zones:
            [
                new ZoneMappingCandidatesV3(
                    ZoneId: ZoneId,
                    Candidates:
                    [
                        new MappingCandidateV3(
                            Longitude: 0.0,
                            Latitude: 0.0,
                            OriginalPosition: null,
                            ProposedRowIndex: 1,
                            ProposedColumnIndex: 1,
                            PositionConfidence: 0.9m)
                    ])
            ]);

        // Act
        var errors = V3BusinessContractValidators.Validate(payloadWithoutCoords);

        // Assert
        Assert.Contains(errors, e => e.Contains("Mapping candidate coordinates (Longitude, Latitude) must be provided"));
    }

    [Fact]
    public void SerializationDeserializationPreservesStreamlinedFields()
    {
        // Arrange
        var original = new BaselineMappingCandidatesReadyV3(
            SurveyOrderId: SurveyOrderId,
            MissionId: MissionId,
            FarmId: FarmId,
            ProducedAt: new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero),
            Zones:
            [
                new ZoneMappingCandidatesV3(
                    ZoneId: ZoneId,
                    GridBearingDeg: 45.0,
                    RowSpacingM: 4.0,
                    PlantSpacingM: 2.5,
                    Candidates:
                    [
                        new MappingCandidateV3(
                            Longitude: 105.789123,
                            Latitude: 21.012345,
                            ProposedRowIndex: 2,
                            ProposedColumnIndex: 3,
                            PositionConfidence: 0.95m,
                            TreeId: "tree_r2_c3")
                    ])
            ]);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Act
        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<BaselineMappingCandidatesReadyV3>(json, options);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.SurveyOrderId, deserialized.SurveyOrderId);
        Assert.Equal(original.MissionId, deserialized.MissionId);
        Assert.Equal(original.FarmId, deserialized.FarmId);
        var zone = Assert.Single(deserialized.Zones);
        var candidate = Assert.Single(zone.Candidates);
        Assert.Equal(105.789123, candidate.Longitude);
        Assert.Equal(21.012345, candidate.Latitude);
        Assert.Equal("tree_r2_c3", candidate.TreeId);
        Assert.Equal(2, candidate.ProposedRowIndex);
        Assert.Equal(3, candidate.ProposedColumnIndex);
    }

    [Fact]
    public void MappingCandidateDomainEntityStoresTreeIdCorrectly()
    {
        // Arrange
        var batchId = Guid.NewGuid();
        var candidate = MappingCandidate.Create(
            id: Guid.NewGuid(),
            batchId: batchId,
            zoneId: ZoneId,
            candidateId: Guid.NewGuid(),
            sourceObservationId: Guid.NewGuid(),
            evidenceMediaAssetIds: [],
            originalPosition: new Point(105.789123, 21.012345) { SRID = 4326 },
            proposedRowIndex: 3,
            proposedColumnIndex: 7,
            positionConfidence: 0.92m,
            boundaryClassification: "IN_BOUNDARY",
            measuredBoundaryDistanceM: 4.2m,
            boundaryThresholdM: 1.0m,
            proposedMatchedPlantId: null,
            matchConfidence: null,
            createdAt: DateTimeOffset.UtcNow,
            treeId: "tree_r3_c7");

        // Assert
        Assert.Equal("tree_r3_c7", candidate.TreeId);
        Assert.Equal(3, candidate.ProposedRowIndex);
        Assert.Equal(7, candidate.ProposedColumnIndex);
        Assert.Equal("IN_BOUNDARY", candidate.BoundaryClassification);
    }
}
