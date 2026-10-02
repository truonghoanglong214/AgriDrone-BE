package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.application.readmodel.UserTenantListItem;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.TenantMembershipJpaEntity;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface TenantMembershipJpaRepository
        extends JpaRepository<TenantMembershipJpaEntity, UUID> {

    List<TenantMembershipJpaEntity> findByUserIdAndStatus(UUID userId, String status);

    Optional<TenantMembershipJpaEntity> findByUserIdAndTenantIdAndStatus(
            UUID userId,
            UUID tenantId,
            String status);

    Optional<TenantMembershipJpaEntity> findByUserIdAndTenantId(
            UUID userId,
            UUID tenantId);

    boolean existsByTenantIdAndRoleAndStatus(UUID tenantId, String role, String status);

    @Query(
            value = """
                    select new com.agridrone.be1.identity.application.readmodel.UserTenantListItem(
                        membership.id,
                        membership.tenantId,
                        membership.role,
                        membership.status,
                        membership.joinedAt,
                        membership.createdAt)
                    from TenantMembershipJpaEntity membership,
                         UserJpaEntity user,
                         TenantJpaEntity tenant
                    where membership.userId = :userId
                      and user.id = membership.userId
                      and tenant.id = membership.tenantId
                      and membership.status = 'ACTIVE'
                      and user.status = com.agridrone.be1.identity.domain.UserStatus.ACTIVE
                      and user.deletedAt is null
                      and tenant.deletedAt is null
                    order by membership.joinedAt desc, membership.id desc
                    """,
            countQuery = """
                    select count(membership.id)
                    from TenantMembershipJpaEntity membership,
                         UserJpaEntity user,
                         TenantJpaEntity tenant
                    where membership.userId = :userId
                      and user.id = membership.userId
                      and tenant.id = membership.tenantId
                      and membership.status = 'ACTIVE'
                      and user.status = com.agridrone.be1.identity.domain.UserStatus.ACTIVE
                      and user.deletedAt is null
                      and tenant.deletedAt is null
                    """)
    Page<UserTenantListItem> findActivePageByUserId(
            @Param("userId") UUID userId,
            Pageable pageable);
}
