package com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter;

import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.TenantJpaEntity;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.TenantJpaRepository;
import java.util.Optional;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Repository;
import org.springframework.transaction.annotation.Transactional;

@Repository
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class TenantPersistenceAdapter implements TenantRepository {
    private final TenantJpaRepository tenants;

    public TenantPersistenceAdapter(TenantJpaRepository tenants) {
        this.tenants = tenants;
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<Tenant> findById(UUID id) {
        return tenants.findByIdAndDeletedAtIsNull(id).map(TenantJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<Tenant> findByCode(String code) {
        return tenants.findByCodeAndDeletedAtIsNull(code).map(TenantJpaEntity::toDomain);
    }

    @Override
    @Transactional
    public void save(Tenant tenant) {
        TenantJpaEntity entity = tenants.findById(tenant.id())
                .orElseGet(() -> new TenantJpaEntity(tenant));
        entity.updateFrom(tenant);
        tenants.saveAndFlush(entity);
    }
}
