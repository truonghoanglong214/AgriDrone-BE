using AgriDrone.Modules.Surveys.Application.Features.Orders.GetManagerSurveyOrderCaptureAttempts;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

public interface ISurveyCaptureAttemptQueries
{
    Task<Result<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>> GetManagerCaptureAttemptsAsync(
        Guid orderId,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
