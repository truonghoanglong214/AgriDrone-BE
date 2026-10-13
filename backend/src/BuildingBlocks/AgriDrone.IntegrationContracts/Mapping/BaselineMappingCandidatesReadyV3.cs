namespace AgriDrone.IntegrationContracts.Mapping;

public sealed record BaselineMappingCandidatesReadyV3(
    Guid CausationId,
    Guid HandoffId,
    Guid SurveyOrderId,
    Guid MissionId,
    Guid FarmId,
    Guid FarmBoundaryVersionId,
    Guid? ExpectedCurrentFarmBaseMapVersionId,
    Guid JobId,
    Guid ModelVersionId,
    string ModelVersion,
    string AlgorithmVersion,
    string BoundaryPolicyVersion,
    DateTimeOffset ProducedAt,
    IReadOnlyList<ZoneMappingCandidatesV3> Zones);

public sealed record ZoneMappingCandidatesV3(
    Guid ZoneId,
    Guid? ExpectedCurrentZoneMapVersionId,
    double GridBearingDeg,
    double RowSpacingM,
    double PlantSpacingM,
    IReadOnlyList<MappingCandidateV3> Candidates);

public sealed record MappingCandidateV3(
    Guid CandidateId,
    Guid SourceObservationId,
    IReadOnlyList<Guid> EvidenceMediaAssetIds,
    GeoJsonPointV3 OriginalPosition,
    int? ProposedRowIndex,
    int? ProposedColumnIndex,
    decimal PositionConfidence,
    string BoundaryClassification,
    decimal MeasuredBoundaryDistanceM,
    decimal BoundaryThresholdM,
    Guid? MatchedPlantId,
    decimal? MatchConfidence);

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
