package com.agridrone.be1.shared.messaging;

import java.util.Map;

public final class IntegrationEventRegistry {
    private static final Map<String, Integer> CONTRACTS = Map.ofEntries(
            Map.entry("mapping.candidates-approved.v1", 1),
            Map.entry("mapping.zone-map-published.v1", 1),
            Map.entry("identity.tenant-invitation-email-requested.v1", 1),
            Map.entry("notification.email-requested.v1", 1),
            Map.entry("health.observations-ready.v1", 1),
            Map.entry("health.review-state-changed.v1", 1),
            Map.entry("mapping.baseline-candidates-approved.v2", 2),
            Map.entry("mapping.farm-base-map-published.v2", 2),
            Map.entry("health.observations-ready.v2", 2),
            Map.entry("harvest-readiness.assessments-ready.v2", 2),
            Map.entry("survey.result-review-state-changed.v2", 2));

    private IntegrationEventRegistry() {
    }

    public static void validate(String eventType, int schemaVersion) {
        Integer expected = CONTRACTS.get(eventType);
        if (expected == null || expected != schemaVersion) {
            throw new IllegalArgumentException("Unsupported event contract: " + eventType + "@" + schemaVersion);
        }
    }

    public static Map<String, Integer> contracts() {
        return CONTRACTS;
    }
}
