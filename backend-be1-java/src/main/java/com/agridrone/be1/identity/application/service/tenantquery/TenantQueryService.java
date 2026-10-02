package com.agridrone.be1.identity.application.service.tenantquery;

import com.agridrone.be1.identity.application.port.in.tenantquery.TenantQueryUseCase;
import com.agridrone.be1.identity.application.port.out.persistence.TenantMembershipRepository;
import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.application.readmodel.TenantListItem;
import com.agridrone.be1.identity.application.readmodel.UserTenantListItem;
import com.agridrone.be1.shared.api.PageRequest;
import com.agridrone.be1.shared.api.PageResponse;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class TenantQueryService implements TenantQueryUseCase {
    private final TenantRepository tenants;
    private final TenantMembershipRepository memberships;

    public TenantQueryService(
            TenantRepository tenants,
            TenantMembershipRepository memberships) {
        this.tenants = tenants;
        this.memberships = memberships;
    }

    @Override
    @Transactional(readOnly = true)
    public PageResponse<TenantListItem> findTenants(PageRequest request) {
        return tenants.findPage(request);
    }

    @Override
    @Transactional(readOnly = true)
    public PageResponse<UserTenantListItem> findUserTenants(
            UUID userId,
            PageRequest request) {
        return memberships.findPageByUserId(userId, request);
    }
}
