package com.agridrone.be1.identity.infrastructure.persistence.jpa.entity;

import com.agridrone.be1.identity.domain.PasswordResetToken;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "password_reset_tokens", schema = "identity")
public class PasswordResetTokenJpaEntity {
    @Id
    private UUID id;

    @Column(name = "user_id", nullable = false)
    private UUID userId;

    @Column(name = "token_hash", nullable = false)
    private String tokenHash;

    @Column(name = "expires_at", nullable = false)
    private Instant expiresAt;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    @Column(name = "used_at")
    private Instant usedAt;

    @Column(name = "revoked_at")
    private Instant revokedAt;

    protected PasswordResetTokenJpaEntity() {}

    public PasswordResetTokenJpaEntity(PasswordResetToken token) {
        id = token.id();
        userId = token.userId();
        tokenHash = token.tokenHash();
        expiresAt = token.expiresAt();
        createdAt = token.createdAt();
        usedAt = token.usedAt();
        revokedAt = token.revokedAt();
    }

    UUID userId() {
        return userId;
    }

    boolean isActiveAt(Instant now) {
        return usedAt == null && revokedAt == null && expiresAt.isAfter(now);
    }

    public void revoke(Instant at) {
        if (usedAt == null && revokedAt == null) {
            revokedAt = at;
        }
    }

    public boolean markUsed(Instant at) {
        if (!isActiveAt(at)) {
            return false;
        }
        usedAt = at;
        return true;
    }

    public PasswordResetToken toDomain() {
        return new PasswordResetToken(
                id,
                userId,
                tokenHash,
                expiresAt,
                createdAt,
                usedAt,
                revokedAt);
    }
}
