package com.agridrone.be1.identity.infrastructure.persistence.jpa.entity;

import com.agridrone.be1.identity.domain.InvitationStatus;
import com.agridrone.be1.identity.domain.TenantInvitation;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "tenant_invitations", schema = "identity")
public class TenantInvitationJpaEntity {
    @Id
    private UUID id;

    @Column(name = "tenant_id", nullable = false)
    private UUID tenantId;

    @Column(nullable = false)
    private String email;

    @Column(nullable = false)
    private String role;

    @Column(nullable = false)
    private String purpose;

    @Column(name = "token_hash", nullable = false)
    private String tokenHash;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private InvitationStatus status;

    @Column(name = "invited_by_user_id", nullable = false)
    private UUID invitedBy;

    @Column(name = "accepted_by_user_id")
    private UUID acceptedBy;

    @Column(name = "expires_at", nullable = false)
    private Instant expiresAt;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    @Column(name = "accepted_at")
    private Instant acceptedAt;

    protected TenantInvitationJpaEntity() {}

    public TenantInvitationJpaEntity(TenantInvitation invitation) {
        id = invitation.id();
        createdAt = invitation.createdAt();
        updateFrom(invitation);
    }

    public void updateFrom(TenantInvitation invitation) {
        tenantId = invitation.tenantId();
        email = invitation.email();
        role = invitation.role();
        purpose = invitation.purpose();
        tokenHash = invitation.tokenHash();
        status = invitation.status();
        invitedBy = invitation.invitedBy();
        acceptedBy = invitation.acceptedBy();
        expiresAt = invitation.expiresAt();
        acceptedAt = invitation.acceptedAt();
    }

    public boolean markAccepted(UUID userId, Instant at) {
        if (status != InvitationStatus.PENDING) {
            return false;
        }
        status = InvitationStatus.ACCEPTED;
        acceptedBy = userId;
        acceptedAt = at;
        return true;
    }

    public TenantInvitation toDomain() {
        return new TenantInvitation(
                id,
                tenantId,
                email,
                role,
                purpose,
                tokenHash,
                status,
                invitedBy,
                acceptedBy,
                expiresAt,
                createdAt,
                acceptedAt);
    }
}
