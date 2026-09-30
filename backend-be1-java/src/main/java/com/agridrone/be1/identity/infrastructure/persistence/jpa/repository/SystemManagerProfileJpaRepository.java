package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.SystemManagerProfileJpaEntity;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface SystemManagerProfileJpaRepository
        extends JpaRepository<SystemManagerProfileJpaEntity, UUID> {

    Optional<SystemManagerProfileJpaEntity> findByUserId(UUID userId);
}
