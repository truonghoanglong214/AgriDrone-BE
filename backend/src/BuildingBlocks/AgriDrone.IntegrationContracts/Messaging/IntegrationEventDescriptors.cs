using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Missions;
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
            RequiresActorId: false,
            RequiresTenantId: false);

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

    public static IntegrationEventDescriptor<BaselineMappingCandidatesReadyV3>
        BaselineMappingCandidatesReadyV3 { get; } =
        new(
            IntegrationEventTypes.BaselineMappingCandidatesReadyV3,
            IntegrationSchemaVersions.V3,
            RequiresActorId: false);

    public static IntegrationEventDescriptor<FarmBaseMapPublishedV3>
        FarmBaseMapPublishedV3 { get; } =
        new(
            IntegrationEventTypes.FarmBaseMapPublishedV3,
            IntegrationSchemaVersions.V3,
            RequiresActorId: true);

    public static IntegrationEventDescriptor<PlantHealthAnalysisReadyV3>
        PlantHealthAnalysisReadyV3 { get; } =
        new(
            IntegrationEventTypes.PlantHealthAnalysisReadyV3,
            IntegrationSchemaVersions.V3,
            RequiresActorId: false);

    public static IntegrationEventDescriptor<SurveyRequestApprovedV1>
        SurveyRequestApprovedV1 { get; } =
        new(
            IntegrationEventTypes.SurveyRequestApprovedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: true);

    public static IntegrationEventDescriptor<SurveyOrderBoundaryVerifiedV1>
        SurveyOrderBoundaryVerifiedV1 { get; } =
        new(
            IntegrationEventTypes.SurveyOrderBoundaryVerifiedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: true);

    public static IntegrationEventDescriptor<SurveyAppointmentChangedV1>
        SurveyAppointmentChangedV1 { get; } =
        new(
            IntegrationEventTypes.SurveyAppointmentChangedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: true);

    public static IntegrationEventDescriptor<SurveyOrderPricingConfirmedV1>
        SurveyOrderPricingConfirmedV1 { get; } =
        new(
            IntegrationEventTypes.SurveyOrderPricingConfirmedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: true);

    public static IntegrationEventDescriptor<SurveyPaymentChangedV1>
        SurveyPaymentChangedV1 { get; } =
        new(
            IntegrationEventTypes.SurveyPaymentChangedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: false);

    public static IntegrationEventDescriptor<SurveyPriceAdjustmentChangedV1>
        SurveyPriceAdjustmentChangedV1 { get; } =
        new(
            IntegrationEventTypes.SurveyPriceAdjustmentChangedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: true);

    public static IntegrationEventDescriptor<SurveyBoundaryCaptureChangedV1>
        SurveyBoundaryCaptureChangedV1 { get; } =
        new(
            IntegrationEventTypes.SurveyBoundaryCaptureChangedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: true);

    public static IntegrationEventDescriptor<BaselineMissionStartedV1>
        BaselineMissionStartedV1 { get; } =
        new(
            IntegrationEventTypes.BaselineMissionStartedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: false);

    public static IntegrationEventDescriptor<BaselineOrthophotoCompletedV1>
        BaselineOrthophotoCompletedV1 { get; } =
        new(
            IntegrationEventTypes.BaselineOrthophotoCompletedV1,
            IntegrationSchemaVersions.V1,
            RequiresActorId: false);
}
