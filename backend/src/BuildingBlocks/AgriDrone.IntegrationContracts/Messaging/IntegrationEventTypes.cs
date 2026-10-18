using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.IntegrationContracts.Messaging
{
    public static class IntegrationEventTypes
    {
        public const string MappingCandidatesApprovedV1 =
        "mapping.candidates-approved.v1";

        public const string ZoneMapPublishedV1 =
            "mapping.zone-map-published.v1";

        public const string TenantInvitationEmailRequestedV1 =
            "identity.tenant-invitation-email-requested.v1";

        public const string EmailNotificationRequestedV1 =
            "notification.email-requested.v1";

        public const string HealthObservationsReadyV1 =
            "health.observations-ready.v1";

        public const string HealthReviewStateChangedV1 =
            "health.review-state-changed.v1";

        public const string BaselineMappingCandidatesApprovedV2 =
            "mapping.baseline-candidates-approved.v2";

        public const string FarmBaseMapPublishedV2 =
            "mapping.farm-base-map-published.v2";

        public const string HealthObservationsReadyV2 =
            "health.observations-ready.v2";

        public const string HarvestReadinessAssessmentsReadyV2 =
            "harvest-readiness.assessments-ready.v2";

        public const string SurveyResultReviewStateChangedV2 =
            "survey.result-review-state-changed.v2";

        public const string BaselineMappingCandidatesReadyV3 =
            "mapping.baseline-candidates-ready.v3";

        public const string FarmBaseMapPublishedV3 =
            "mapping.farm-base-map-published.v3";

        public const string PlantHealthAnalysisReadyV3 =
            "health.analysis-ready.v3";

        public const string SurveyRequestApprovedV1 =
            "survey.request-approved.v1";

        public const string SurveyOrderBoundaryVerifiedV1 =
            "survey.order-boundary-verified.v1";

        public const string SurveyAppointmentChangedV1 =
            "survey.appointment-changed.v1";

        public const string SurveyOrderPricingConfirmedV1 =
            "survey.order-pricing-confirmed.v1";

        public const string SurveyPaymentChangedV1 =
            "survey.payment-changed.v1";

        public const string SurveyPriceAdjustmentChangedV1 =
            "survey.price-adjustment-changed.v1";

        public const string SurveyBoundaryCaptureChangedV1 =
            "survey.boundary-capture-changed.v1";
    }
}
