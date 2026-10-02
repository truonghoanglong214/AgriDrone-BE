package com.agridrone.be1.identity.api.tenant;

import java.time.Instant;
import java.util.UUID;

public record TenantListItemResponse(
        UUID id,
        String code,
        String name,
        int status,
        Instant createdAt) {
}
