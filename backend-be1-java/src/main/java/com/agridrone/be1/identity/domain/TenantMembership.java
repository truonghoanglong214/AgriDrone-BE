package com.agridrone.be1.identity.domain;

import java.time.Instant;
import java.util.UUID;

public record TenantMembership(
        UUID id,
        UUID tenantId,
        UUID userId,
        String role,
        String status,
        Instant joinedAt,
        Instant createdAt,
        long version) {

    public TenantMembership {
        if (id == null || tenantId == null || userId == null) {
            throw new IllegalArgumentException("Membership identity is required");
        }
        if (!"OWNER".equals(role)) {
            throw new IllegalArgumentException("Only OWNER membership is supported");
        }
        if (version < 1) {
            throw new IllegalArgumentException("version must be positive");
        }
    }

    public static TenantMembership owner(UUID tenantId, UUID userId, Instant now) {
        return new TenantMembership(
                UUID.randomUUID(),
                tenantId,
                userId,
                "OWNER",
                "ACTIVE",
                now,
                now,
                1);
    }

    public boolean isActive() {
        return "ACTIVE".equals(status);
    }
}
