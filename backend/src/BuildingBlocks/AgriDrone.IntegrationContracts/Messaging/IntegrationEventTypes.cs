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

        public const string HealthObservationsReadyV1 =
            "health.observations-ready.v1";

        public const string HealthReviewStateChangedV1 =
            "health.review-state-changed.v1";
    }
}
