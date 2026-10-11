using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;

public sealed record ApproveSurveyRequestCommand(
    Guid SurveyRequestId,
    Guid? SelectedSystemManagerProfileId,
    string Reason,
    SurveyRequestReviewChecklistInput Checklist,
    uint ExpectedVersion,
    string IdempotencyKey)
    : IRequest<Result<ApproveSurveyRequestResponse>>;
