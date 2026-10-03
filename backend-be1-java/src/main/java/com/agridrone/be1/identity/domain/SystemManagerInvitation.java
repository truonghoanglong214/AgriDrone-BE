package com.agridrone.be1.identity.domain;

import java.time.Instant;
import java.util.Locale;
import java.util.Objects;
import java.util.UUID;

public record SystemManagerInvitation(
        UUID id,
        String email,
        String tokenHash,
        InvitationStatus status,
        UUID invitedBy,
        UUID acceptedBy,
        Instant expiresAt,
        Instant createdAt,
        Instant acceptedAt) {

    public SystemManagerInvitation {
        Objects.requireNonNull(id, "id");
        email = requireText(email, "email").toLowerCase(Locale.ROOT);
        tokenHash = requireText(tokenHash, "tokenHash");
        Objects.requireNonNull(status, "status");
        Objects.requireNonNull(invitedBy, "invitedBy");
        Objects.requireNonNull(expiresAt, "expiresAt");
        Objects.requireNonNull(createdAt, "createdAt");
        if (!expiresAt.isAfter(createdAt)) {
            throw new IllegalArgumentException("expiresAt must be after createdAt");
        }
        if (status == InvitationStatus.ACCEPTED
                && (acceptedBy == null || acceptedAt == null)) {
            throw new IllegalArgumentException(
                    "Accepted invitations require acceptedBy and acceptedAt");
        }
    }

    public static SystemManagerInvitation create(
            String email,
            String tokenHash,
            UUID invitedBy,
            Instant expiresAt,
            Instant now) {
        return new SystemManagerInvitation(
                UUID.randomUUID(),
                email,
                tokenHash,
                InvitationStatus.PENDING,
                invitedBy,
                null,
                expiresAt,
                now,
                null);
    }

    public boolean canAccept(Instant now) {
        return status == InvitationStatus.PENDING && expiresAt.isAfter(now);
    }

    public SystemManagerInvitation accept(UUID userId, Instant now) {
        if (!canAccept(now)) {
            throw new IllegalStateException(
                    "Only a pending, unexpired invitation can be accepted");
        }
        return new SystemManagerInvitation(
                id, email, tokenHash, InvitationStatus.ACCEPTED, invitedBy,
                Objects.requireNonNull(userId), expiresAt, createdAt,
                Objects.requireNonNull(now));
    }

    public SystemManagerInvitation expire(Instant now) {
        if (status != InvitationStatus.PENDING || now.isBefore(expiresAt)) {
            throw new IllegalStateException(
                    "Only an expired pending invitation can be marked expired");
        }
        return new SystemManagerInvitation(
                id, email, tokenHash, InvitationStatus.EXPIRED, invitedBy,
                null, expiresAt, createdAt, null);
    }

    private static String requireText(String value, String name) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException(name + " is required");
        }
        return value.trim();
    }
}
