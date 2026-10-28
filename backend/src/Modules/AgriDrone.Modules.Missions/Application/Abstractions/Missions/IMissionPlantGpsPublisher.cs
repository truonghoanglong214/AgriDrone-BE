using AgriDrone.IntegrationContracts.Mapping;

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
        BaselineMappingCandidatesReadyV3 payload,
        CancellationToken cancellationToken);
}

public sealed record MissionPlantGpsPublishResult(
    MissionPlantGpsPublishStatus Status,
    Guid? MessageId = null,
    IReadOnlyList<string>? Errors = null);

public enum MissionPlantGpsPublishStatus
{
    Queued,
    NotFound,
    Conflict,
    Invalid
}
