using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.RejectSurveyRequest;

public sealed record RejectSurveyRequestResponse(
    Guid Id,
    string RequestNumber,
    SurveyRequestStatus Status,
    uint Version,
    Guid ReviewId,
    DateTimeOffset ReviewedAt);
