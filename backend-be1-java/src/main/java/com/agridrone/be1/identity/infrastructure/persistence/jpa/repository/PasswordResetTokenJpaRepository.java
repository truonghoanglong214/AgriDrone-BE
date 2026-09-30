package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.PasswordResetTokenJpaEntity;
import jakarta.persistence.LockModeType;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;

public interface PasswordResetTokenJpaRepository
        extends JpaRepository<PasswordResetTokenJpaEntity, UUID> {

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    Optional<PasswordResetTokenJpaEntity> findByTokenHash(String tokenHash);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    List<PasswordResetTokenJpaEntity> findByUserIdAndUsedAtIsNullAndRevokedAtIsNull(
            UUID userId);

    @Override
    @Lock(LockModeType.PESSIMISTIC_WRITE)
    Optional<PasswordResetTokenJpaEntity> findById(UUID id);
}
