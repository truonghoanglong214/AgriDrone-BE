package com.agridrone.be1.identity.domain;

import java.time.Instant;
import java.util.UUID;

public record TenantInvitation(
        UUID id,
        UUID tenantId,
        String email,
        String tokenHash,
        InvitationStatus status,
        UUID invitedBy,
        UUID acceptedBy,
        Instant expiresAt,
        Instant createdAt,
        Instant acceptedAt) {

    public boolean canAccept(Instant now) {
        return status == InvitationStatus.PENDING && expiresAt.isAfter(now);
    }
}
