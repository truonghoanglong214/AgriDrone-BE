package com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter;

import com.agridrone.be1.identity.application.port.out.persistence.TenantInvitationRepository;
import com.agridrone.be1.identity.domain.TenantInvitation;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.TenantInvitationJpaEntity;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.TenantInvitationJpaRepository;
import java.time.Instant;
import java.util.Optional;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Repository;
import org.springframework.transaction.annotation.Transactional;

@Repository
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class TenantInvitationPersistenceAdapter implements TenantInvitationRepository {
    private static final String OWNER = TenantInvitation.OWNER_ROLE;
    private static final String OWNER_PROVISIONING =
            TenantInvitation.OWNER_PROVISIONING_PURPOSE;

    private final TenantInvitationJpaRepository invitations;

    public TenantInvitationPersistenceAdapter(TenantInvitationJpaRepository invitations) {
        this.invitations = invitations;
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<TenantInvitation> findByTokenHash(String tokenHash) {
        return invitations.findByTokenHash(tokenHash).map(TenantInvitationJpaEntity::toDomain);
    }

    @Override
    @Transactional
    public Optional<TenantInvitation> findByTokenHashForUpdate(String tokenHash) {
        return invitations.findByTokenHashForUpdate(tokenHash)
                .map(TenantInvitationJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<TenantInvitation> findPendingOwnerProvisioning(UUID tenantId) {
        return invitations
                .findFirstByTenantIdAndRoleAndPurposeAndStatusOrderByCreatedAtDesc(
                        tenantId,
                        OWNER,
                        OWNER_PROVISIONING,
                        com.agridrone.be1.identity.domain.InvitationStatus.PENDING)
                .map(TenantInvitationJpaEntity::toDomain);
    }

    @Override
    @Transactional
    public void add(TenantInvitation invitation) {
        invitations.saveAndFlush(new TenantInvitationJpaEntity(invitation));
    }

    @Override
    @Transactional
    public void save(TenantInvitation invitation) {
        TenantInvitationJpaEntity entity = invitations.findById(invitation.id())
                .orElseGet(() -> new TenantInvitationJpaEntity(invitation));
        entity.updateFrom(invitation);
        invitations.saveAndFlush(entity);
    }

    @Override
    @Transactional
    public boolean markAccepted(UUID invitationId, UUID userId, Instant acceptedAt) {
        Optional<TenantInvitationJpaEntity> existing = invitations.findById(invitationId);
        if (existing.isEmpty() || !existing.get().markAccepted(userId, acceptedAt)) {
            return false;
        }

        invitations.saveAndFlush(existing.get());
        return true;
    }
}
