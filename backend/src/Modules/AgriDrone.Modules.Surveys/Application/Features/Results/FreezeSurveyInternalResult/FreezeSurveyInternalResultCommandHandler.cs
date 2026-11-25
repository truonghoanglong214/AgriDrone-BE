using AgriDrone.Modules.Surveys.Application.Abstractions.Results;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Results.FreezeSurveyInternalResult;

internal sealed class FreezeSurveyInternalResultCommandHandler(
    ISurveyResultReviewTransaction transaction,
    IExecutionContext executionContext)
    : IRequestHandler<FreezeSurveyInternalResultCommand, Result<FreezeSurveyInternalResultResponse>>
{
    public async Task<Result<FreezeSurveyInternalResultResponse>> Handle(
        FreezeSurveyInternalResultCommand request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized || executionContext.ActorId is not Guid actorId || actorId == Guid.Empty)
        {
            return Result.Failure<FreezeSurveyInternalResultResponse>(
                AppError.Unauthorized("FreezeResult.Unauthorized", "An authenticated assigned SystemManager is required."));
        }

        return await transaction.FreezeInternalResultAsync(request, actorId, cancellationToken);
    }
}
