namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record SurveyAuthorizationEvidenceUrlApiResponse(
    Guid SurveyOrderId,
    string Type,
    string Status,
    string EvidenceReference,
    bool IsMock,
    string? DownloadUrl,
    DateTimeOffset? UrlExpiresAt,
    string? Note);
