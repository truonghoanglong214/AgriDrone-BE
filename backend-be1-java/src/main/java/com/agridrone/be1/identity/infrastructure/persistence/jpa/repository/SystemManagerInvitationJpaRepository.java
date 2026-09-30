package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.SystemManagerInvitationJpaEntity;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;

public interface SystemManagerInvitationJpaRepository
        extends JpaRepository<SystemManagerInvitationJpaEntity, UUID> {

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    Optional<SystemManagerInvitationJpaEntity> findByTokenHash(String tokenHash);

    @Override
    @Lock(LockModeType.PESSIMISTIC_WRITE)
    Optional<SystemManagerInvitationJpaEntity> findById(UUID id);
}
