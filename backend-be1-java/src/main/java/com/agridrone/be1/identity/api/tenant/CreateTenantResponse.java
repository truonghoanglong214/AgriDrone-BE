package com.agridrone.be1.identity.api.tenant;

import java.time.Instant;
import java.util.UUID;

public record CreateTenantResponse(
        UUID tenantId,
        String code,
        String name,
        int status,
        Instant createdAt) {
}
