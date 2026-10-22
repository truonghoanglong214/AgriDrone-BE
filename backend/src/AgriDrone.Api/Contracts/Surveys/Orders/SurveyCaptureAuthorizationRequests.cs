using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record VerifySurveyCaptureAuthorizationRequest(
    [param: Required, StringLength(1000, MinimumLength = 1)]
    string EvidenceReference,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    uint ExpectedVersion);

public sealed record RevokeSurveyCaptureAuthorizationRequest(
    [param: Required, StringLength(2000, MinimumLength = 1)]
    string Reason,
    uint ExpectedVersion);

public sealed record SurveyCaptureAuthorizationApiResponse(
    Guid Id,
    Guid SurveyOrderId,
    string Type,
    string Status,
    string? EvidenceReference,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    Guid? VerifiedBy,
    uint Version);
