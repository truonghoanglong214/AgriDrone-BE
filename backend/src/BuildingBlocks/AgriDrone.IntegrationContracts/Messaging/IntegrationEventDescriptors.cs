using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.HarvestReadiness;
using AgriDrone.IntegrationContracts.Surveys;

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

    public static IntegrationEventDescriptor<EmailNotificationRequestedV1>
        EmailNotificationRequestedV1 { get; } =
        new(
            IntegrationEventTypes.EmailNotificationRequestedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: false);

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

    public static IntegrationEventDescriptor<BaselineMappingCandidatesApprovedV2>
        BaselineMappingCandidatesApprovedV2 { get; } =
        new(
            IntegrationEventTypes.BaselineMappingCandidatesApprovedV2,
            IntegrationSchemaVersions.V2,
            RequiresActorId: true);

    public static IntegrationEventDescriptor<FarmBaseMapPublishedV2>
        FarmBaseMapPublishedV2 { get; } =
        new(
            IntegrationEventTypes.FarmBaseMapPublishedV2,
            IntegrationSchemaVersions.V2,
            RequiresActorId: true);

    public static IntegrationEventDescriptor<HealthObservationsReadyV2>
        HealthObservationsReadyV2 { get; } =
        new(
            IntegrationEventTypes.HealthObservationsReadyV2,
            IntegrationSchemaVersions.V2,
            RequiresActorId: false);

    public static IntegrationEventDescriptor<HarvestReadinessAssessmentsReadyV2>
        HarvestReadinessAssessmentsReadyV2 { get; } =
        new(
            IntegrationEventTypes.HarvestReadinessAssessmentsReadyV2,
            IntegrationSchemaVersions.V2,
            RequiresActorId: false);

    public static IntegrationEventDescriptor<SurveyResultReviewStateChangedV2>
        SurveyResultReviewStateChangedV2 { get; } =
        new(
            IntegrationEventTypes.SurveyResultReviewStateChangedV2,
            IntegrationSchemaVersions.V2,
            RequiresActorId: false);
}
