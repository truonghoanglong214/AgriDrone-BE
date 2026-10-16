namespace AgriDrone.Modules.Missions.Application.Abstractions.Processing;

public interface IMissionProcessor
{
    Task ProcessMissionAsync(
        MissionProcessingWorkItem item,
        CancellationToken cancellationToken = default);
}
