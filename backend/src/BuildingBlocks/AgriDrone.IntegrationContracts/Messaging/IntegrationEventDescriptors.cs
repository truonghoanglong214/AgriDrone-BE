using AgriDrone.IntegrationContracts.Mapping;

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
}
