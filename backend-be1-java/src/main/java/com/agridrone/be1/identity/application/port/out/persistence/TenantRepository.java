package com.agridrone.be1.identity.application.port.out.persistence;

import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.application.readmodel.TenantListItem;
import com.agridrone.be1.shared.api.PageRequest;
import com.agridrone.be1.shared.api.PageResponse;
import java.util.Optional;
import java.util.UUID;

public interface TenantRepository {
    Optional<Tenant> findById(UUID id);

    Optional<Tenant> findByIdIncludingInactive(UUID id);

    Optional<Tenant> findByCode(String code);

    boolean existsByNormalizedCode(String normalizedCode);

    PageResponse<TenantListItem> findPage(PageRequest pageRequest);

    void save(Tenant tenant);
}
