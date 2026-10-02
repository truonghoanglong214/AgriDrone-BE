package com.agridrone.be1.identity.infrastructure.security;

import com.agridrone.be1.identity.application.error.TenantErrorCodes;
import com.agridrone.be1.identity.application.port.out.persistence.TenantMembershipRepository;
import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.shared.auth.EffectiveTenantAccessGuard;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

@Component
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class EffectiveTenantAccessGuardAdapter implements EffectiveTenantAccessGuard {
    private final UserRepository users;
    private final TenantRepository tenants;
    private final TenantMembershipRepository memberships;

    public EffectiveTenantAccessGuardAdapter(
            UserRepository users,
            TenantRepository tenants,
            TenantMembershipRepository memberships) {
        this.users = users;
        this.tenants = tenants;
        this.memberships = memberships;
    }

    @Override
    @Transactional(readOnly = true)
    public Decision evaluate(UUID userId, UUID tenantId, UUID membershipId) {
        if (users.findById(userId).filter(User::isActive).isEmpty()) {
            return denied();
        }
        var membership = memberships.findActive(userId, tenantId).orElse(null);
        if (membership == null || !membership.id().equals(membershipId)) {
            return denied();
        }
        Tenant tenant = tenants.findByIdIncludingInactive(tenantId).orElse(null);
        if (tenant == null) {
            return denied();
        }
        if (!tenant.isActive()) {
            return Decision.deny(TenantErrorCodes.INACTIVE, "The tenant is inactive.");
        }
        return Decision.allow();
    }

    private static Decision denied() {
        return Decision.deny(
                TenantErrorCodes.ACCESS_DENIED,
                "The user does not have active access to the tenant.");
    }
}
