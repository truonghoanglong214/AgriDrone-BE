using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Authorizations.GetSurveyCaptureAuthorizationDetail;

public sealed record GetSurveyCaptureAuthorizationDetailQuery(
    Guid SurveyOrderId,
    SurveyCaptureAuthorizationType Type)
    : IRequest<Result<SurveyCaptureAuthorizationDetailResponse>>;

public sealed record SurveyCaptureAuthorizationDetailResponse(
    Guid Id,
    Guid SurveyOrderId,
    SurveyCaptureAuthorizationType Type,
    SurveyCaptureAuthorizationStatus Status,
    string? EvidenceReference,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    Guid? VerifiedBy,
    uint Version);
