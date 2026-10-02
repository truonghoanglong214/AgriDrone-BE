package com.agridrone.be1.identity.api.tenant;

import java.time.Instant;
import java.util.UUID;

public record UserTenantListItemResponse(
        UUID id,
        UUID tenantId,
        int role,
        int status,
        Instant joinedAt,
        Instant createdAt) {
}
