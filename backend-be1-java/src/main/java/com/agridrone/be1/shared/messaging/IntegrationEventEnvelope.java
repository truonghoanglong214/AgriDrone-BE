package com.agridrone.be1.shared.messaging;

import com.fasterxml.jackson.databind.JsonNode;
import java.time.OffsetDateTime;
import java.util.UUID;

public record IntegrationEventEnvelope(
        UUID messageId,
        UUID correlationId,
        UUID tenantId,
        UUID actorId,
        OffsetDateTime occurredAt,
        int schemaVersion,
        String eventType,
        JsonNode payload) {

    public IntegrationEventEnvelope {
        if (messageId == null || correlationId == null || tenantId == null || occurredAt == null
                || eventType == null || eventType.isBlank() || payload == null || schemaVersion < 1) {
            throw new IllegalArgumentException("Integration-event envelope is incomplete");
        }
    }
}
