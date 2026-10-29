namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public interface IMissionPlantGpsPublisher
{
    Task<MissionPlantGpsPublishResult> PublishAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid actorId,
        Guid correlationId,
        Guid idempotencyKey,
        MissionPlantGpsPublishRequest request,
        CancellationToken cancellationToken);
}
