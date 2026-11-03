using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionStartedOutbox(
    MissionsDbContext db,
    OutboxMessageFactory factory) : IMissionStartedOutbox
{
    public void Enqueue(
        BaselineMissionStartedV1 payload,
        Guid actorId,
        Guid correlationId)
        => Enqueue(
            IntegrationEventDescriptors.BaselineMissionStartedV1,
            payload,
            payload.SurveyOrderId,
            payload.TenantId,
            payload.StartedAt,
            actorId,
            correlationId);

    public void Enqueue(
        PaidMissionStartedV1 payload,
        Guid actorId,
        Guid correlationId)
        => Enqueue(
            IntegrationEventDescriptors.PaidMissionStartedV1,
            payload,
            payload.SurveyOrderId,
            payload.TenantId,
            payload.StartedAt,
            actorId,
            correlationId);

    private void Enqueue<TPayload>(
        IntegrationEventDescriptor<TPayload> descriptor,
        TPayload payload,
        Guid surveyOrderId,
        Guid tenantId,
        DateTimeOffset startedAt,
        Guid actorId,
        Guid correlationId)
    {
        var envelope = IntegrationEventEnvelopeFactory.Create(
            descriptor,
            Guid.NewGuid(),
            correlationId,
            tenantId,
            actorId,
            startedAt,
            payload);

        db.OutboxMessages.Add(factory.Create(
            envelope,
            descriptor.EventType,
            surveyOrderId.ToString("D")));
    }
}
