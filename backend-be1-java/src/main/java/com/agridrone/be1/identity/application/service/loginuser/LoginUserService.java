package com.agridrone.be1.identity.application.service.loginuser;

import com.agridrone.be1.identity.application.error.AuthenticationErrorCodes;
import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserCommand;
import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserResult;
import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserUseCase;
import com.agridrone.be1.identity.application.port.out.persistence.TenantMembershipRepository;
import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.AccessTokenIssuer;
import com.agridrone.be1.identity.application.security.IssuedAccessToken;
import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.domain.TenantMembership;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.identity.domain.SystemRoleCodes;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import com.agridrone.be1.shared.error.StableApiException;

@Service
@ConditionalOnProperty(
        prefix = "agridrone.security.jwt.issuer-service",
        name = "enabled",
        havingValue = "true")
public class LoginUserService implements LoginUserUseCase {
    private final UserRepository users;
    private final TenantMembershipRepository memberships;
    private final TenantRepository tenants;
    private final PasswordHasher passwordHasher;
    private final AccessTokenIssuer tokenIssuer;
    private final Clock clock;

    public LoginUserService(
            UserRepository users,
            TenantMembershipRepository memberships,
            TenantRepository tenants,
            PasswordHasher passwordHasher,
            AccessTokenIssuer tokenIssuer,
            Clock clock) {
        this.users = users;
        this.memberships = memberships;
        this.tenants = tenants;
        this.passwordHasher = passwordHasher;
        this.tokenIssuer = tokenIssuer;
        this.clock = clock;
    }

    @Override
    @Transactional
    public LoginUserResult login(LoginUserCommand command) {
        String email = command.email().trim().toLowerCase(Locale.ROOT);
        User user = users.findByEmail(email).orElseThrow(this::invalidCredentials);
        if (!user.isActive() || !passwordHasher.matches(command.password(), user.passwordHash())) {
            throw invalidCredentials();
        }

        Set<String> roles = users.findSystemRoleCodes(user.id());
        Instant now = clock.instant();
        if (roles.contains(SystemRoleCodes.SYSTEM_ADMIN)
                || roles.contains(SystemRoleCodes.SYSTEM_MANAGER)) {
            return issueSession(user, roles, null, now);
        }

        List<TenantMembership> activeMemberships = memberships.findActiveByUserId(user.id());
        if (activeMemberships.isEmpty()) {
            throw new StableApiException(
                    HttpStatus.FORBIDDEN,
                    AuthenticationErrorCodes.NO_TENANT_MEMBERSHIP,
                    "The user does not belong to an active tenant.");
        }
        if (activeMemberships.size() > 1) {
            throw new StableApiException(
                    HttpStatus.CONFLICT,
                    AuthenticationErrorCodes.TENANT_SELECTION_REQUIRED,
                    "Tenant selection is required before creating a session.");
        }

        TenantMembership membership = activeMemberships.getFirst();
        Tenant tenant = tenants.findById(membership.tenantId())
                .filter(Tenant::isActive)
                .orElseThrow(() -> new StableApiException(
                        HttpStatus.FORBIDDEN,
                        AuthenticationErrorCodes.NO_TENANT_MEMBERSHIP,
                        "The user does not belong to an active tenant."));
        return issueSession(user, roles, new TenantSession(membership, tenant), now);
    }

    private LoginUserResult issueSession(
            User user,
            Set<String> roles,
            TenantSession tenantSession,
            Instant now) {
        UUIDParts context = tenantSession == null
                ? new UUIDParts(null, null, null)
                : new UUIDParts(
                        tenantSession.membership().tenantId(),
                        tenantSession.membership().id(),
                        tenantSession.membership().role());
        IssuedAccessToken token = tokenIssuer.issue(
                user.id(), context.tenantId(), context.membershipId(), context.tenantRole(),
                roles.stream().sorted().toList());
        user.recordLogin(now);
        users.save(user);
        LoginUserResult.Tenant tenant = tenantSession == null
                ? null
                : new LoginUserResult.Tenant(
                        tenantSession.tenant().id(),
                        tenantSession.tenant().code(),
                        tenantSession.tenant().name(),
                        tenantSession.membership().role());
        return new LoginUserResult(
                user.email(), user.fullName(), user.phone(),
                new LoginUserResult.Session(token.value(), token.expiresAt(), tenant));
    }

    private StableApiException invalidCredentials() {
        return new StableApiException(
                HttpStatus.UNAUTHORIZED,
                AuthenticationErrorCodes.INVALID_CREDENTIALS,
                "Invalid email or password.");
    }

    private record TenantSession(TenantMembership membership, Tenant tenant) {
    }

    private record UUIDParts(
            java.util.UUID tenantId,
            java.util.UUID membershipId,
            String tenantRole) {
    }
}
