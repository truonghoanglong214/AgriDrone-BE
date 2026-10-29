namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapDraft;

public sealed record CandidateDto(
    Guid CandidateId,
    Guid SourceObservationId,
    IReadOnlyList<Guid> EvidenceMediaAssetIds,
    double Longitude,
    double Latitude,
    int? ProposedRowIndex,
    int? ProposedColumnIndex,
    decimal PositionConfidence,
    string BoundaryClassification,
    string ReviewDecision,
    double? CorrectedLongitude,
    double? CorrectedLatitude,
    int? CorrectedRowIndex,
    int? CorrectedColumnIndex,
    Guid? ProposedMatchedPlantId,
    Guid? CorrectedMatchedPlantId,
    string? ReviewReason,
    DateTimeOffset? ReviewedAt,
    string? TreeId = null);
