using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.StartSurveyRequestReview;

public sealed record StartSurveyRequestReviewCommand(
    Guid SurveyRequestId,
    uint ExpectedVersion)
    : IRequest<Result<StartSurveyRequestReviewResponse>>;
