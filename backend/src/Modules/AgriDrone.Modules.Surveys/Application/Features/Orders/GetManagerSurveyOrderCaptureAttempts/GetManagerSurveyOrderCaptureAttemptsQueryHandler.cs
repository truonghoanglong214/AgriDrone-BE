using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetManagerSurveyOrderCaptureAttempts;

internal sealed class GetManagerSurveyOrderCaptureAttemptsQueryHandler(
    ISurveyCaptureAttemptQueries queries,
    IExecutionContext executionContext)
    : IRequestHandler<
        GetManagerSurveyOrderCaptureAttemptsQuery,
        Result<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>>
{
    public async Task<Result<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>> Handle(
        GetManagerSurveyOrderCaptureAttemptsQuery request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            actorId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>(
                AppError.Unauthorized(
                    "CaptureAttempts.Unauthorized",
                    "An authenticated SystemManager actor is required."));
        }

        return await queries.GetManagerCaptureAttemptsAsync(
            request.OrderId,
            actorId,
            cancellationToken);
    }
}
