package com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter;

import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.application.readmodel.TenantListItem;
import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.TenantJpaEntity;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.TenantJpaRepository;
import com.agridrone.be1.shared.api.PageRequest;
import com.agridrone.be1.shared.api.PageResponse;
import java.util.Locale;
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
    public Optional<Tenant> findByIdIncludingInactive(UUID id) {
        return tenants.findByIdAndDeletedAtIsNull(id).map(TenantJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<Tenant> findByCode(String code) {
        return tenants.findByCodeAndDeletedAtIsNull(code).map(TenantJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public boolean existsByNormalizedCode(String normalizedCode) {
        return tenants.existsByCodeAndDeletedAtIsNull(
                normalizedCode.trim().toUpperCase(Locale.ROOT));
    }

    @Override
    @Transactional(readOnly = true)
    public PageResponse<TenantListItem> findPage(PageRequest pageRequest) {
        var page = tenants.findPage(org.springframework.data.domain.PageRequest.of(
                pageRequest.zeroBasedPageIndex(),
                pageRequest.pageSize()));
        return PageResponse.of(page.getContent(), pageRequest, page.getTotalElements());
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
