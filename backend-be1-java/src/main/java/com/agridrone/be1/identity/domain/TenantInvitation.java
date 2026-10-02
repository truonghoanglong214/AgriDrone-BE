package com.agridrone.be1.identity.domain;

import java.time.Instant;
import java.util.Locale;
import java.util.UUID;

public record TenantInvitation(
        UUID id,
        UUID tenantId,
        String email,
        String role,
        String purpose,
        String tokenHash,
        InvitationStatus status,
        UUID invitedBy,
        UUID acceptedBy,
        Instant expiresAt,
        Instant createdAt,
        Instant acceptedAt) {

    public static final String OWNER_ROLE = "OWNER";
    public static final String OWNER_PROVISIONING_PURPOSE = "OWNER_PROVISIONING";

    public TenantInvitation {
        if (id == null || tenantId == null || invitedBy == null) {
            throw new IllegalArgumentException("Invitation identity is required");
        }
        if (email == null || email.isBlank()) {
            throw new IllegalArgumentException("Invitation email is required");
        }
        if (!OWNER_ROLE.equals(role)) {
            throw new IllegalArgumentException("Only OWNER invitations are supported");
        }
        if (!OWNER_PROVISIONING_PURPOSE.equals(purpose)) {
            throw new IllegalArgumentException(
                    "Only OWNER_PROVISIONING invitations are supported");
        }
        if (tokenHash == null || tokenHash.length() != 64) {
            throw new IllegalArgumentException("Invitation token hash must be SHA-256");
        }
        if (status == null || expiresAt == null || createdAt == null) {
            throw new IllegalArgumentException("Invitation lifecycle is required");
        }
        email = email.trim().toLowerCase(Locale.ROOT);
        if (!expiresAt.isAfter(createdAt)) {
            throw new IllegalArgumentException("Invitation expiry must follow creation");
        }
        if (status == InvitationStatus.ACCEPTED
                && (acceptedBy == null || acceptedAt == null)) {
            throw new IllegalArgumentException("Accepted invitation details are required");
        }
        if (status != InvitationStatus.ACCEPTED && acceptedAt != null) {
            throw new IllegalArgumentException("Only accepted invitations have an acceptance time");
        }
    }

    public static TenantInvitation createOwnerProvisioning(
            UUID tenantId,
            String email,
            String tokenHash,
            UUID invitedBy,
            Instant expiresAt,
            Instant createdAt) {
        return new TenantInvitation(
                UUID.randomUUID(),
                tenantId,
                email,
                OWNER_ROLE,
                OWNER_PROVISIONING_PURPOSE,
                tokenHash,
                InvitationStatus.PENDING,
                invitedBy,
                null,
                expiresAt,
                createdAt,
                null);
    }

    public boolean canAccept(Instant now) {
        return status == InvitationStatus.PENDING && expiresAt.isAfter(now);
    }

    public TenantInvitation expire(Instant now) {
        if (status != InvitationStatus.PENDING || now.isBefore(expiresAt)) {
            throw new IllegalStateException("Only an expired pending invitation can be expired");
        }
        return new TenantInvitation(
                id,
                tenantId,
                email,
                role,
                purpose,
                tokenHash,
                InvitationStatus.EXPIRED,
                invitedBy,
                acceptedBy,
                expiresAt,
                createdAt,
                acceptedAt);
    }

    public TenantInvitation accept(UUID userId, Instant now) {
        if (userId == null || !canAccept(now)) {
            throw new IllegalStateException("Only a pending unexpired invitation can be accepted");
        }
        return new TenantInvitation(
                id,
                tenantId,
                email,
                role,
                purpose,
                tokenHash,
                InvitationStatus.ACCEPTED,
                invitedBy,
                userId,
                expiresAt,
                createdAt,
                now);
    }
}
