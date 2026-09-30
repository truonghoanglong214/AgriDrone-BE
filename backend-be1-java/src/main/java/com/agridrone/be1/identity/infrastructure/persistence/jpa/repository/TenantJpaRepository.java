package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.TenantJpaEntity;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface TenantJpaRepository extends JpaRepository<TenantJpaEntity, UUID> {
    Optional<TenantJpaEntity> findByIdAndDeletedAtIsNull(UUID id);

    Optional<TenantJpaEntity> findByCodeAndDeletedAtIsNull(String code);
}
