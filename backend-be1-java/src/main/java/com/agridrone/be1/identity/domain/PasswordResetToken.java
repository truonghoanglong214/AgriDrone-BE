package com.agridrone.be1.identity.domain;

import java.time.Instant;
import java.util.UUID;

public record PasswordResetToken(
        UUID id,
        UUID userId,
        String tokenHash,
        Instant expiresAt,
        Instant createdAt,
        Instant usedAt,
        Instant revokedAt) {

    public boolean canUse(Instant now) {
        return usedAt == null && revokedAt == null && expiresAt.isAfter(now);
    }
}
