namespace AgriDrone.IntegrationContracts.Mapping;

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
    decimal? MatchConfidence = null,
    string? Status = null,
    string? Source = null);
