using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.StartSurveyRequestReview;

public sealed record StartSurveyRequestReviewResponse(
    Guid Id,
    string RequestNumber,
    SurveyRequestStatus Status,
    uint Version,
    DateTimeOffset UpdatedAt);
