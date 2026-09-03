using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.IntegrationContracts.Health;

namespace AgriDrone.IntegrationContracts.Messaging;

public static class IntegrationEventDescriptors
{
    public static IntegrationEventDescriptor<MappingCandidatesApprovedV1>
        MappingCandidatesApprovedV1 { get; } =
        new(
            IntegrationEventTypes.MappingCandidatesApprovedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: true);

    public static IntegrationEventDescriptor<ZoneMapPublishedV1>
        ZoneMapPublishedV1 { get; } =
        new(
            IntegrationEventTypes.ZoneMapPublishedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: true);

    public static IntegrationEventDescriptor<TenantInvitationEmailRequestedV1>
        TenantInvitationEmailRequestedV1 { get; } =
        new(
            IntegrationEventTypes.TenantInvitationEmailRequestedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: true);

    public static IntegrationEventDescriptor<HealthObservationsReadyV1>
        HealthObservationsReadyV1
        { get; } =
        new(
            IntegrationEventTypes.HealthObservationsReadyV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: false);

    public static IntegrationEventDescriptor<HealthReviewStateChangedV1>
        HealthReviewStateChangedV1
        { get; } =
        new(
            IntegrationEventTypes.HealthReviewStateChangedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: false);
}
