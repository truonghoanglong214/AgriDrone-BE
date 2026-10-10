using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.GetSurveyRequestInbox;

public sealed record GetSurveyRequestInboxQuery(
    SurveyRequestStatus? Status,
    SurveyRequestKind? Kind,
    Guid? SurveyServiceId,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedTo,
    int PageNumber = 1,
    int PageSize = 20)
    : IRequest<Result<PagedResult<SurveyRequestInboxItemResponse>>>;
