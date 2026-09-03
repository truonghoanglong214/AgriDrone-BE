using AgriDrone.IntegrationContracts.AI;

namespace AgriDrone.Modules.Missions.Application.Abstractions.AI;

public interface IAiJobClient
{
    Task<AiJobSubmissionResult> SubmitAsync(
        AiJobRequestV1 request,
        CancellationToken cancellationToken = default);

    Task CancelAsync(
        string externalJobId,
        CancellationToken cancellationToken = default);
}