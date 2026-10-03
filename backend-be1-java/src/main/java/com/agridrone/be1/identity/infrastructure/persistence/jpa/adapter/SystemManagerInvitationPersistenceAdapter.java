package com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter;

import com.agridrone.be1.identity.application.port.out.persistence.SystemManagerInvitationRepository;
import com.agridrone.be1.identity.domain.SystemManagerInvitation;
import com.agridrone.be1.identity.domain.InvitationStatus;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.SystemManagerInvitationJpaEntity;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.SystemManagerInvitationJpaRepository;
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
public class SystemManagerInvitationPersistenceAdapter
        implements SystemManagerInvitationRepository {

    private final SystemManagerInvitationJpaRepository invitations;

    public SystemManagerInvitationPersistenceAdapter(
            SystemManagerInvitationJpaRepository invitations) {
        this.invitations = invitations;
    }

    @Override
    @Transactional
    public Optional<SystemManagerInvitation> findByTokenHashForUpdate(String tokenHash) {
        return invitations.findByTokenHashForUpdate(tokenHash)
                .map(SystemManagerInvitationJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<SystemManagerInvitation> findByTokenHash(String tokenHash) {
        return invitations.findByTokenHash(tokenHash)
                .map(SystemManagerInvitationJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<SystemManagerInvitation> findPendingByEmail(String email) {
        return invitations
                .findFirstByEmailIgnoreCaseAndStatusOrderByCreatedAtDesc(
                        email, InvitationStatus.PENDING)
                .map(SystemManagerInvitationJpaEntity::toDomain);
    }

    @Override
    @Transactional
    public void add(SystemManagerInvitation invitation) {
        invitations.saveAndFlush(new SystemManagerInvitationJpaEntity(invitation));
    }

    @Override
    @Transactional
    public void save(SystemManagerInvitation invitation) {
        SystemManagerInvitationJpaEntity entity = invitations.findById(invitation.id())
                .orElseThrow(() -> new IllegalArgumentException(
                        "System Manager invitation does not exist"));
        entity.updateFrom(invitation);
        invitations.saveAndFlush(entity);
    }

    @Override
    @Transactional
    public boolean markAccepted(UUID invitationId, UUID userId, Instant acceptedAt) {
        Optional<SystemManagerInvitationJpaEntity> existing = invitations.findById(invitationId);
        if (existing.isEmpty() || !existing.get().markAccepted(userId, acceptedAt)) {
            return false;
        }

        invitations.saveAndFlush(existing.get());
        return true;
    }
}
