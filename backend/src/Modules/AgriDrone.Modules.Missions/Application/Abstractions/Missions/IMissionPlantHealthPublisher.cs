namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public interface IMissionPlantHealthPublisher
{
    Task<MissionPlantHealthPublishResult> PublishAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid actorId,
        Guid correlationId,
        Guid idempotencyKey,
        CancellationToken cancellationToken);
}

