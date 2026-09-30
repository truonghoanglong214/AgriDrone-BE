package com.agridrone.be1.shared.audit;

import com.fasterxml.jackson.databind.JsonNode;
import java.util.UUID;

public record AuditEntry(
        UUID farmId,
        String entityType,
        UUID entityId,
        String action,
        JsonNode before,
        JsonNode after,
        String reason) {

    public AuditEntry {
        if (entityType == null || entityType.isBlank() || action == null || action.isBlank()) {
            throw new IllegalArgumentException("entityType and action are required");
        }
    }
}
