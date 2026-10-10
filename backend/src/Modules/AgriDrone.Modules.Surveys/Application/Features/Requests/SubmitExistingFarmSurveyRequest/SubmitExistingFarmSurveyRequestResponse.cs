using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitExistingFarmSurveyRequest;

public sealed record SubmitExistingFarmSurveyRequestResponse(
    Guid Id,
    string RequestNumber,
    SurveyRequestKind Kind,
    SurveyRequestStatus Status,
    DateTimeOffset CreatedAt);
