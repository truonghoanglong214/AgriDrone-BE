package com.agridrone.be1.identity.application.port.out.persistence;

import com.agridrone.be1.identity.domain.Tenant;
import java.util.Optional;
import java.util.UUID;

public interface TenantRepository {
    Optional<Tenant> findById(UUID id);

    Optional<Tenant> findByCode(String code);

    void save(Tenant tenant);
}
