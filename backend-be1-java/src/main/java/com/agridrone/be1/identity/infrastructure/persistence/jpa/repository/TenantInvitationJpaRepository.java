package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.TenantInvitationJpaEntity;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;

public interface TenantInvitationJpaRepository
        extends JpaRepository<TenantInvitationJpaEntity, UUID> {

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    Optional<TenantInvitationJpaEntity> findByTokenHash(String tokenHash);

    @Override
    @Lock(LockModeType.PESSIMISTIC_WRITE)
    Optional<TenantInvitationJpaEntity> findById(UUID id);
}
