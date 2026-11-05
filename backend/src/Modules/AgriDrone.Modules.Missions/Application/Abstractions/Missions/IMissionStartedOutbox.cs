using AgriDrone.IntegrationContracts.Missions;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

internal interface IMissionStartedOutbox
{
    void Enqueue(
        BaselineMissionStartedV1 payload,
        Guid actorId,
        Guid correlationId);

    void Enqueue(
        PaidMissionStartedV1 payload,
        Guid actorId,
        Guid correlationId);
}
