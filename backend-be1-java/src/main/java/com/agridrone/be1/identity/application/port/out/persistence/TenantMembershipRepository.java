package com.agridrone.be1.identity.application.port.out.persistence;

import com.agridrone.be1.identity.domain.TenantMembership;
import com.agridrone.be1.identity.application.readmodel.UserTenantListItem;
import com.agridrone.be1.shared.api.PageRequest;
import com.agridrone.be1.shared.api.PageResponse;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface TenantMembershipRepository {
    List<TenantMembership> findActiveByUserId(UUID userId);

    Optional<TenantMembership> findActive(UUID userId, UUID tenantId);

    Optional<TenantMembership> find(UUID userId, UUID tenantId);

    boolean hasActiveOwner(UUID tenantId);

    PageResponse<UserTenantListItem> findPageByUserId(
            UUID userId,
            PageRequest pageRequest);

    void add(TenantMembership membership);
}
