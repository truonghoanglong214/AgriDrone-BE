package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.TenantMembershipJpaEntity;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface TenantMembershipJpaRepository
        extends JpaRepository<TenantMembershipJpaEntity, UUID> {

    List<TenantMembershipJpaEntity> findByUserIdAndStatus(UUID userId, String status);

    Optional<TenantMembershipJpaEntity> findByUserIdAndTenantIdAndStatus(
            UUID userId,
            UUID tenantId,
            String status);

    boolean existsByTenantIdAndRoleAndStatus(UUID tenantId, String role, String status);
}
