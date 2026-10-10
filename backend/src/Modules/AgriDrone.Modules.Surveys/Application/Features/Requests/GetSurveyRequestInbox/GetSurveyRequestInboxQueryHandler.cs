using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.GetSurveyRequestInbox;

internal sealed class GetSurveyRequestInboxQueryHandler(
    ISurveyRequestQueries requestQueries,
    IExecutionContext executionContext)
    : IRequestHandler<
        GetSurveyRequestInboxQuery,
        Result<PagedResult<SurveyRequestInboxItemResponse>>>
{
    public async Task<Result<PagedResult<SurveyRequestInboxItemResponse>>> Handle(
        GetSurveyRequestInboxQuery request,
        CancellationToken cancellationToken)
    {
        if (!HasAuthenticatedAdminContext(executionContext))
        {
            return Result.Failure<PagedResult<SurveyRequestInboxItemResponse>>(
                SurveyRequestError.CurrentSystemAdminRequired());
        }

        var page = await requestQueries.GetSystemAdminInboxAsync(
            new SurveyRequestInboxFilter(
                request.Status,
                request.Kind,
                request.SurveyServiceId,
                request.CreatedFrom,
                request.CreatedTo),
            new PagedRequest(request.PageNumber, request.PageSize),
            cancellationToken);

        var items = page.Items
            .Select(item => new SurveyRequestInboxItemResponse(
                item.Id,
                item.RequestNumber,
                item.Kind,
                item.Status,
                item.SurveyServiceId,
                item.SurveyServiceCode,
                item.TenantId,
                item.FarmId,
                item.ApplicantName,
                item.FarmName,
                item.ApproximateAreaHa,
                item.EstimatedPoleCount,
                item.CreatedAt,
                item.UpdatedAt,
                item.Version))
            .ToArray();

        return Result.Success(new PagedResult<SurveyRequestInboxItemResponse>(
            items,
            page.PageNumber,
            page.PageSize,
            page.TotalCount));
    }

    private static bool HasAuthenticatedAdminContext(
        IExecutionContext executionContext) =>
        executionContext.IsInitialized &&
        executionContext.ActorId.HasValue &&
        executionContext.CorrelationId != Guid.Empty;
}
