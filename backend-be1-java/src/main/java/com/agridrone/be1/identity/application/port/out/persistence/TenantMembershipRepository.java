package com.agridrone.be1.identity.application.port.out.persistence;

import com.agridrone.be1.identity.domain.TenantMembership;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface TenantMembershipRepository {
    List<TenantMembership> findActiveByUserId(UUID userId);

    Optional<TenantMembership> findActive(UUID userId, UUID tenantId);

    boolean hasActiveOwner(UUID tenantId);

    void add(TenantMembership membership);
}
