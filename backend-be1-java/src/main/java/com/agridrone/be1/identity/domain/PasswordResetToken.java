package com.agridrone.be1.identity.domain;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

public record PasswordResetToken(
        UUID id,
        UUID userId,
        String tokenHash,
        Instant expiresAt,
        Instant createdAt,
        Instant usedAt,
        Instant revokedAt) {

    public static PasswordResetToken create(
            UUID userId,
            String tokenHash,
            Instant expiresAt,
            Instant createdAt) {
        Objects.requireNonNull(userId, "userId");
        Objects.requireNonNull(expiresAt, "expiresAt");
        Objects.requireNonNull(createdAt, "createdAt");
        if (tokenHash == null || tokenHash.isBlank()) {
            throw new IllegalArgumentException("tokenHash is required");
        }
        if (!expiresAt.isAfter(createdAt)) {
            throw new IllegalArgumentException("expiresAt must be after createdAt");
        }
        return new PasswordResetToken(
                UUID.randomUUID(), userId, tokenHash, expiresAt, createdAt, null, null);
    }

    public boolean canUse(Instant now) {
        return usedAt == null && revokedAt == null && expiresAt.isAfter(now);
    }
}
