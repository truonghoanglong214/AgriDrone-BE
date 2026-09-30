package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.RoleJpaEntity;

import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface RoleJpaRepository extends JpaRepository<RoleJpaEntity, UUID> {
    Optional<RoleJpaEntity> findByCode(String code);
}
