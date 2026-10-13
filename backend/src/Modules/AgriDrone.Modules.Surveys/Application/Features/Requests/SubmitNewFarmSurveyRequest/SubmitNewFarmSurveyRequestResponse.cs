using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewFarmSurveyRequest;

public sealed record SubmitNewFarmSurveyRequestResponse(
    Guid Id,
    string RequestNumber,
    SurveyRequestKind Kind,
    SurveyRequestStatus Status,
    DateTimeOffset CreatedAt);
