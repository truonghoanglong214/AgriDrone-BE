package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.TenantInvitationJpaEntity;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface TenantInvitationJpaRepository
        extends JpaRepository<TenantInvitationJpaEntity, UUID> {

    Optional<TenantInvitationJpaEntity> findByTokenHash(String tokenHash);

    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("""
            select invitation
            from TenantInvitationJpaEntity invitation
            where invitation.tokenHash = :tokenHash
            """)
    Optional<TenantInvitationJpaEntity> findByTokenHashForUpdate(
            @Param("tokenHash") String tokenHash);

    Optional<TenantInvitationJpaEntity>
            findFirstByTenantIdAndRoleAndPurposeAndStatusOrderByCreatedAtDesc(
                    UUID tenantId,
                    String role,
                    String purpose,
                    com.agridrone.be1.identity.domain.InvitationStatus status);

    @Override
    @Lock(LockModeType.PESSIMISTIC_WRITE)
    Optional<TenantInvitationJpaEntity> findById(UUID id);
}
