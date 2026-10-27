using AgriDrone.SharedKernel.Application;
using MediatR;

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
    DateTimeOffset? ReviewedAt);

public sealed record CandidateZoneDto(
    Guid ZoneId,
    double GridBearingDeg,
    double RowSpacingM,
    double PlantSpacingM,
    IReadOnlyList<CandidateDto> Candidates);

public sealed record FarmBaseMapDraftResponse(
    Guid DraftId,
    Guid FarmId,
    int VersionNumber,
    string Status,
    Guid SourceSurveyOrderId,
    Guid? SourceMissionId,
    Guid? FarmBoundaryVersionId,
    Guid? HandoffId,
    Guid? BatchId,
    string? BatchStatus,
    Guid? JobId,
    string? ModelVersion,
    string? AlgorithmVersion,
    DateTimeOffset? ProducedAt,
    int TotalCandidates,
    int PendingCount,
    int AcceptedCount,
    int RejectedCount,
    int RelocatedCount,
    int ReplacedCount,
    IReadOnlyList<CandidateZoneDto> Zones);

public sealed record GetFarmBaseMapDraftQuery(Guid FarmId, Guid DraftId)
    : IRequest<Result<FarmBaseMapDraftResponse>>;
