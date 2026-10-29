using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.SharedKernel.Application;
using MediatR;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.ReviewMappingCandidate;

public sealed record CandidateReviewResponse(
    Guid CandidateId,
    string ReviewDecision,
    double? CorrectedLongitude,
    double? CorrectedLatitude,
    int? CorrectedRowIndex,
    int? CorrectedColumnIndex,
    Guid? CorrectedMatchedPlantId,
    string? Reason,
    DateTimeOffset ReviewedAt);

public sealed record ReviewMappingCandidateCommand(
    Guid FarmId,
    Guid DraftId,
    Guid CandidateId,
    MappingCandidateReviewDecision Decision,
    Point? CorrectedPosition,
    int? CorrectedRowIndex,
    int? CorrectedColumnIndex,
    Guid? CorrectedMatchedPlantId,
    string? Reason) : IRequest<Result<CandidateReviewResponse>>;
