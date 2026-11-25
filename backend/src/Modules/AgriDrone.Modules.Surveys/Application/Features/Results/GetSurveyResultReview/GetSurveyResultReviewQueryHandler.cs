using AgriDrone.Modules.Surveys.Application.Abstractions.Results;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Results.GetSurveyResultReview;

internal sealed class GetSurveyResultReviewQueryHandler(
    ISurveyResultReviewTransaction transaction,
    IExecutionContext executionContext)
    : IRequestHandler<GetSurveyResultReviewQuery, Result<SurveyResultReviewResponse>>
{
    public async Task<Result<SurveyResultReviewResponse>> Handle(
        GetSurveyResultReviewQuery request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized || executionContext.ActorId is not Guid actorId || actorId == Guid.Empty)
        {
            return Result.Failure<SurveyResultReviewResponse>(
                AppError.Unauthorized("SurveyResultReview.Unauthorized", "An authenticated assigned SystemManager is required."));
        }

        return await transaction.GetReviewAsync(request.OrderId, actorId, cancellationToken);
    }
}
