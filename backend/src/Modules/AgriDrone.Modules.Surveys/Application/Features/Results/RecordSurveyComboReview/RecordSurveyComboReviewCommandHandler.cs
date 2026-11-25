using AgriDrone.Modules.Surveys.Application.Abstractions.Results;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Results.RecordSurveyComboReview;

internal sealed class RecordSurveyComboReviewCommandHandler(
    ISurveyResultReviewTransaction transaction,
    IExecutionContext executionContext)
    : IRequestHandler<RecordSurveyComboReviewCommand, Result<RecordSurveyComboReviewResponse>>
{
    public async Task<Result<RecordSurveyComboReviewResponse>> Handle(
        RecordSurveyComboReviewCommand request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized || executionContext.ActorId is not Guid actorId || actorId == Guid.Empty)
        {
            return Result.Failure<RecordSurveyComboReviewResponse>(
                AppError.Unauthorized("SurveyComboReview.Unauthorized", "An authenticated assigned SystemManager is required."));
        }

        return await transaction.RecordComboReviewAsync(request, actorId, cancellationToken);
    }
}
