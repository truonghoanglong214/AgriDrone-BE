using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.SharedKernel.Application;
using MediatR;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.ReviewMappingCandidatesBatch;

public sealed record CandidateBatchReviewItemDto(
    Guid CandidateId,
    MappingCandidateReviewDecision Decision,
    Point? CorrectedPosition,
    int? CorrectedRowIndex,
    int? CorrectedColumnIndex,
    Guid? CorrectedMatchedPlantId,
    string? Reason);

public sealed record BatchReviewSummaryResponse(
    int TotalReviewed,
    int AcceptedCount,
    int RejectedCount,
    int RelocatedCount,
    int ReplacedCount,
    int RemainingPendingCount);

public sealed record ReviewMappingCandidatesBatchCommand(
    Guid FarmId,
    Guid DraftId,
    IReadOnlyList<CandidateBatchReviewItemDto> Reviews,
    string? BatchReason) : IRequest<Result<BatchReviewSummaryResponse>>;
