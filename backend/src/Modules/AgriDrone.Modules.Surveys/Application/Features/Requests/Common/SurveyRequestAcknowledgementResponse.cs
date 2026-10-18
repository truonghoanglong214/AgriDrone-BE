using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.Common;

public sealed record SurveyRequestAcknowledgementResponse(
    Guid Id,
    string RequestNumber,
    string? TenantCode,
    SurveyRequestKind Kind,
    SurveyRequestStatus Status,
    DateTimeOffset CreatedAt);
