using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.RejectSurveyRequest;

public sealed record RejectSurveyRequestCommand(
    Guid SurveyRequestId,
    string Reason,
    SurveyRequestReviewChecklistInput Checklist,
    uint ExpectedVersion)
    : IRequest<Result<RejectSurveyRequestResponse>>;
