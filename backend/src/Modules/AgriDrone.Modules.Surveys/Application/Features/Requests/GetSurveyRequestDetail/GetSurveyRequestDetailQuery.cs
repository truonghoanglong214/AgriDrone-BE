using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.GetSurveyRequestDetail;

public sealed record GetSurveyRequestDetailQuery(Guid SurveyRequestId)
    : IRequest<Result<SurveyRequestDetailResponse>>;
