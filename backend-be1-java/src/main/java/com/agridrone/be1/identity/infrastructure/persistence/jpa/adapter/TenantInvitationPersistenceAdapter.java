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
    private final TenantInvitationJpaRepository invitations;

    public TenantInvitationPersistenceAdapter(TenantInvitationJpaRepository invitations) {
        this.invitations = invitations;
    }

    @Override
    @Transactional
    public Optional<TenantInvitation> findByTokenHashForUpdate(String tokenHash) {
        return invitations.findByTokenHash(tokenHash).map(TenantInvitationJpaEntity::toDomain);
    }

    @Override
    @Transactional
    public void add(TenantInvitation invitation) {
        invitations.saveAndFlush(new TenantInvitationJpaEntity(invitation));
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
