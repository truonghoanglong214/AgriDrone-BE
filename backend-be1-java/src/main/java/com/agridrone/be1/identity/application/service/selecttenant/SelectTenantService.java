package com.agridrone.be1.identity.application.service.selecttenant;

import com.agridrone.be1.identity.application.error.AuthenticationErrorCodes;
import com.agridrone.be1.identity.application.error.TenantErrorCodes;
import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserResult;
import com.agridrone.be1.identity.application.port.in.selecttenant.SelectTenantUseCase;
import com.agridrone.be1.identity.application.port.out.persistence.TenantMembershipRepository;
import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.AccessTokenIssuer;
import com.agridrone.be1.identity.application.port.out.security.TenantSelectionTokenService;
import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.shared.error.StableApiException;
import java.util.Optional;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@ConditionalOnProperty(
        prefix = "agridrone.security.jwt.issuer-service",
        name = "enabled",
        havingValue = "true")
public class SelectTenantService implements SelectTenantUseCase {
    private final TenantSelectionTokenService selectionTokens;
    private final TenantMembershipRepository memberships;
    private final TenantRepository tenants;
    private final UserRepository users;
    private final AccessTokenIssuer accessTokens;

    public SelectTenantService(
            TenantSelectionTokenService selectionTokens,
            TenantMembershipRepository memberships,
            TenantRepository tenants,
            UserRepository users,
            AccessTokenIssuer accessTokens) {
        this.selectionTokens = selectionTokens;
        this.memberships = memberships;
        this.tenants = tenants;
        this.users = users;
        this.accessTokens = accessTokens;
    }

    @Override
    @Transactional(readOnly = true)
    public LoginUserResult select(SelectTenantCommand command) {
        var userId = selectionTokens.validate(command.selectionToken())
                .orElseThrow(SelectTenantService::invalidSelectionToken);
        User user = users.findById(userId)
                .filter(User::isActive)
                .orElseThrow(SelectTenantService::invalidSelectionToken);
        var membership = memberships.findActive(user.id(), command.tenantId())
                .orElseThrow(SelectTenantService::tenantAccessDenied);
        Tenant tenant = tenants.findById(membership.tenantId())
                .filter(Tenant::isActive)
                .orElseThrow(SelectTenantService::tenantAccessDenied);
        var systemRoles = users.findSystemRoleCodes(user.id()).stream().sorted().toList();
        var token = accessTokens.issue(
                user.id(),
                tenant.id(),
                membership.id(),
                membership.role(),
                systemRoles);
        return new LoginUserResult(
                user.email(),
                user.fullName(),
                user.phone(),
                new LoginUserResult.Session(
                        token.value(),
                        token.expiresAt(),
                        new LoginUserResult.Tenant(
                                tenant.id(), tenant.code(), tenant.name(), membership.role())),
                null);
    }

    private static StableApiException invalidSelectionToken() {
        return new StableApiException(
                HttpStatus.UNPROCESSABLE_ENTITY,
                AuthenticationErrorCodes.INVALID_TENANT_SELECTION_TOKEN,
                "The tenant-selection token is invalid or expired.");
    }

    private static StableApiException tenantAccessDenied() {
        return new StableApiException(
                HttpStatus.FORBIDDEN,
                TenantErrorCodes.ACCESS_DENIED,
                "The user does not have the required access to the selected tenant.");
    }
}
