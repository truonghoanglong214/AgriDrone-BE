package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.application.readmodel.TenantListItem;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.TenantJpaEntity;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;

public interface TenantJpaRepository extends JpaRepository<TenantJpaEntity, UUID> {
    Optional<TenantJpaEntity> findByIdAndDeletedAtIsNull(UUID id);

    Optional<TenantJpaEntity> findByCodeAndDeletedAtIsNull(String code);

    boolean existsByCodeAndDeletedAtIsNull(String code);

    @Query(
            value = """
                    select new com.agridrone.be1.identity.application.readmodel.TenantListItem(
                        tenant.id,
                        tenant.code,
                        tenant.name,
                        tenant.status,
                        tenant.createdAt)
                    from TenantJpaEntity tenant
                    where tenant.deletedAt is null
                    order by tenant.createdAt desc, tenant.id desc
                    """,
            countQuery = """
                    select count(tenant.id)
                    from TenantJpaEntity tenant
                    where tenant.deletedAt is null
                    """)
    Page<TenantListItem> findPage(Pageable pageable);
}
