using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Authorizations.VerifySurveyCaptureAuthorization;

public sealed record VerifySurveyCaptureAuthorizationCommand(
    Guid SurveyOrderId,
    SurveyCaptureAuthorizationType Type,
    string EvidenceReference,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    uint ExpectedVersion) : IRequest<Result<SurveyCaptureAuthorizationResponse>>;

public sealed record SurveyCaptureAuthorizationResponse(
    Guid Id,
    Guid SurveyOrderId,
    SurveyCaptureAuthorizationType Type,
    SurveyCaptureAuthorizationStatus Status,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    uint Version);
