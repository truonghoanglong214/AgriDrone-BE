namespace AgriDrone.IntegrationContracts.Mapping;

public sealed record BaselineMappingCandidatesReadyV3(
    Guid SurveyOrderId,
    Guid MissionId,
    Guid FarmId,
    IReadOnlyList<ZoneMappingCandidatesV3> Zones,
    Guid? FarmBoundaryVersionId = null,
    Guid? CausationId = null,
    Guid? HandoffId = null,
    Guid? ExpectedCurrentFarmBaseMapVersionId = null,
    Guid? JobId = null,
    Guid? ModelVersionId = null,
    string ModelVersion = "yolo-v8",
    string AlgorithmVersion = "orchard-grid-v1",
    string BoundaryPolicyVersion = "v1",
    DateTimeOffset? ProducedAt = null);

public sealed record ZoneMappingCandidatesV3(
    Guid ZoneId,
    IReadOnlyList<MappingCandidateV3> Candidates,
    Guid? ExpectedCurrentZoneMapVersionId = null,
    double GridBearingDeg = 0.0,
    double RowSpacingM = 3.0,
    double PlantSpacingM = 2.5);

public sealed record MappingCandidateV3(
    double Longitude = 0.0,
    double Latitude = 0.0,
    int? ProposedRowIndex = null,
    int? ProposedColumnIndex = null,
    decimal PositionConfidence = 1.0m,
    string? TreeId = null,
    Guid? CandidateId = null,
    Guid? SourceObservationId = null,
    IReadOnlyList<Guid>? EvidenceMediaAssetIds = null,
    GeoJsonPointV3? OriginalPosition = null,
    string? BoundaryClassification = null,
    decimal? MeasuredBoundaryDistanceM = null,
    decimal? BoundaryThresholdM = null,
    Guid? MatchedPlantId = null,
    decimal? MatchConfidence = null);

public sealed record GeoJsonPointV3(
    string Type,
    IReadOnlyList<double> Coordinates);

public sealed record GeoJsonPolygonV3(
    string Type,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>> Coordinates);

public static class BoundaryClassificationsV3
{
    public const string InBoundary = "IN_BOUNDARY";
    public const string OutOfBoundary = "OUT_OF_BOUNDARY";
    public const string NeedsReview = "NEEDS_REVIEW";
}
