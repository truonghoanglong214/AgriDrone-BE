package com.agridrone.be1.identity.application.port.in.tenantquery;

import com.agridrone.be1.identity.application.readmodel.TenantListItem;
import com.agridrone.be1.identity.application.readmodel.UserTenantListItem;
import com.agridrone.be1.shared.api.PageRequest;
import com.agridrone.be1.shared.api.PageResponse;
import java.util.UUID;

public interface TenantQueryUseCase {
    PageResponse<TenantListItem> findTenants(PageRequest request);

    PageResponse<UserTenantListItem> findUserTenants(UUID userId, PageRequest request);
}
