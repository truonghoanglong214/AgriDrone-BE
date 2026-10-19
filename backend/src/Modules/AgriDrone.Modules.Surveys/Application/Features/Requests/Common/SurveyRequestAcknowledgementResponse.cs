using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.Common;

public sealed record SurveyRequestAcknowledgementResponse(
    Guid Id,
    string RequestNumber,
    string? TenantCode,
    string? TenantName,
    SurveyRequestKind Kind,
    SurveyRequestStatus Status,
    DateTimeOffset CreatedAt);
