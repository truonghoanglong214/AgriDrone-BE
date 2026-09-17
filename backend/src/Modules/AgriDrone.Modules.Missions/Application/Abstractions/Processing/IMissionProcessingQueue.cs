namespace AgriDrone.Modules.Missions.Application.Abstractions.Processing;

public interface IMissionProcessingQueue
{
    ValueTask EnqueueAsync(
        MissionProcessingWorkItem workItem,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<MissionProcessingWorkItem> ReadAllAsync(
        CancellationToken cancellationToken = default);
}
