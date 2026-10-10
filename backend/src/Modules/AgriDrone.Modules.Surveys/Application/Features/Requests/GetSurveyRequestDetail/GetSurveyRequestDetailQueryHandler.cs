using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.GetSurveyRequestDetail;

internal sealed class GetSurveyRequestDetailQueryHandler(
    ISurveyRequestQueries requestQueries,
    IExecutionContext executionContext)
    : IRequestHandler<
        GetSurveyRequestDetailQuery,
        Result<SurveyRequestDetailResponse>>
{
    public async Task<Result<SurveyRequestDetailResponse>> Handle(
        GetSurveyRequestDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            !executionContext.ActorId.HasValue ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<SurveyRequestDetailResponse>(
                SurveyRequestError.CurrentSystemAdminRequired());
        }

        var detail = await requestQueries.GetSystemAdminDetailAsync(
            request.SurveyRequestId,
            cancellationToken);
        if (detail is null)
        {
            return Result.Failure<SurveyRequestDetailResponse>(
                SurveyRequestError.NotFound(request.SurveyRequestId));
        }

        try
        {
            var reviews = detail.Reviews
                .Select(review => new SurveyRequestReviewResponse(
                    review.Id,
                    review.Decision,
                    review.ChecklistSnapshot.RootElement.Clone(),
                    review.Reason,
                    review.ReviewedBy,
                    review.ReviewedAt))
                .ToArray();

            return Result.Success(new SurveyRequestDetailResponse(
                detail.Id,
                detail.RequestNumber,
                detail.Kind,
                detail.Status,
                detail.TenantId,
                detail.FarmId,
                detail.RequestedByUserId,
                detail.SurveyServiceId,
                detail.SurveyServiceCode,
                detail.SurveyServiceName,
                detail.ApplicantName,
                detail.ApplicantEmail,
                detail.ApplicantPhone,
                detail.FarmName,
                detail.FarmAddress,
                detail.ApproximateAreaHa,
                new SurveyRequestCandidateLocationResponse(
                    detail.Longitude,
                    detail.Latitude,
                    detail.MapSrid),
                detail.EstimatedPoleCount,
                detail.PreferredStartAt,
                detail.PreferredEndAt,
                detail.Notes,
                detail.Version,
                detail.CreatedAt,
                detail.UpdatedAt,
                SurveyRequestReviewChecklistDefinition.Version,
                SurveyRequestReviewChecklistDefinition.Items,
                reviews));
        }
        finally
        {
            foreach (var review in detail.Reviews)
            {
                review.ChecklistSnapshot.Dispose();
            }
        }
    }
}
