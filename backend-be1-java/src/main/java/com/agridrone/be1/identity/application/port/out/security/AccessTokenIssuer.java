package com.agridrone.be1.identity.application.port.out.security;

import java.util.List;
import java.util.UUID;
import com.agridrone.be1.identity.application.security.IssuedAccessToken;

public interface AccessTokenIssuer {
    IssuedAccessToken issue(UUID subject, UUID tenantId, UUID tenantMembershipId,
            String tenantRole, List<String> systemRoles);
}
