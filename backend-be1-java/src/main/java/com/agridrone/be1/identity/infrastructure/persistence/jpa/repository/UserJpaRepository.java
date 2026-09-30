package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.domain.UserStatus;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.UserJpaEntity;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface UserJpaRepository extends JpaRepository<UserJpaEntity, UUID> {
    Optional<UserJpaEntity> findByEmailIgnoreCaseAndDeletedAtIsNull(String email);

    Optional<UserJpaEntity> findByEmailIgnoreCase(String email);

    Optional<UserJpaEntity> findByIdAndDeletedAtIsNull(UUID id);

    boolean existsByRoles_CodeAndStatusAndDeletedAtIsNull(String roleCode, UserStatus status);
}
