package com.agridrone.be1.identity.application.readmodel;

import java.time.Instant;
import java.util.UUID;

public record UserTenantListItem(
        UUID id,
        UUID tenantId,
        String role,
        String status,
        Instant joinedAt,
        Instant createdAt) {
}
