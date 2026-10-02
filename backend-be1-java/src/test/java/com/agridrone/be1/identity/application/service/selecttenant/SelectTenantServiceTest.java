package com.agridrone.be1.identity.application.service.selecttenant;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatExceptionOfType;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import com.agridrone.be1.identity.application.error.AuthenticationErrorCodes;
import com.agridrone.be1.identity.application.error.TenantErrorCodes;
import com.agridrone.be1.identity.application.port.in.selecttenant.SelectTenantUseCase;
import com.agridrone.be1.identity.application.port.out.persistence.TenantMembershipRepository;
import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.AccessTokenIssuer;
import com.agridrone.be1.identity.application.port.out.security.TenantSelectionTokenService;
import com.agridrone.be1.identity.application.security.IssuedAccessToken;
import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.domain.TenantMembership;
import com.agridrone.be1.identity.domain.TenantStatus;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.identity.domain.UserStatus;
import com.agridrone.be1.shared.error.StableApiException;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.Set;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

class SelectTenantServiceTest {
    private static final Instant NOW = Instant.parse("2026-10-02T00:00:00Z");
    private final UserRepository users = org.mockito.Mockito.mock(UserRepository.class);
    private final TenantRepository tenants = org.mockito.Mockito.mock(TenantRepository.class);
    private final TenantMembershipRepository memberships =
            org.mockito.Mockito.mock(TenantMembershipRepository.class);
    private final TenantSelectionTokenService selectionTokens =
            org.mockito.Mockito.mock(TenantSelectionTokenService.class);
    private final AccessTokenIssuer accessTokens =
            org.mockito.Mockito.mock(AccessTokenIssuer.class);
    private final SelectTenantService service = new SelectTenantService(
            selectionTokens, memberships, tenants, users, accessTokens);
    private final UUID userId = UUID.randomUUID();
    private final UUID tenantId = UUID.randomUUID();

    @BeforeEach
    void tokenIsValidByDefault() {
        when(selectionTokens.validate("selection-token")).thenReturn(Optional.of(userId));
    }

    @Test
    void reloadsAuthorizationStateAndIssuesTenantAccessToken() {
        User user = user(UserStatus.ACTIVE);
        Tenant tenant = tenant(TenantStatus.ACTIVE);
        TenantMembership membership = TenantMembership.owner(tenantId, userId, NOW);
        when(users.findById(userId)).thenReturn(Optional.of(user));
        when(users.findSystemRoleCodes(userId)).thenReturn(Set.of("SYSTEM_MANAGER"));
        when(memberships.findActive(userId, tenantId)).thenReturn(Optional.of(membership));
        when(tenants.findById(tenantId)).thenReturn(Optional.of(tenant));
        when(accessTokens.issue(
                        userId, tenantId, membership.id(), "OWNER", List.of("SYSTEM_MANAGER")))
                .thenReturn(new IssuedAccessToken("access-token", NOW, NOW.plusSeconds(900)));

        var result = service.select(command());

        assertThat(result.tenantSelection()).isNull();
        assertThat(result.session().accessToken()).isEqualTo("access-token");
        assertThat(result.session().tenant().id()).isEqualTo(tenantId);
        assertThat(result.session().tenant().role()).isEqualTo("OWNER");
    }

    @Test
    void rejectsInvalidTokenAndInactiveUserAsInvalidSelectionToken() {
        when(selectionTokens.validate("bad-token")).thenReturn(Optional.empty());
        assertError(
                () -> service.select(new SelectTenantUseCase.SelectTenantCommand(
                        "bad-token", tenantId)),
                AuthenticationErrorCodes.INVALID_TENANT_SELECTION_TOKEN);

        when(users.findById(userId)).thenReturn(Optional.of(user(UserStatus.INACTIVE)));
        assertError(() -> service.select(command()),
                AuthenticationErrorCodes.INVALID_TENANT_SELECTION_TOKEN);
        verifyNoInteractions(accessTokens);
    }

    @Test
    void rejectsInactiveOrForeignMembershipAndInactiveTenantAsAccessDenied() {
        when(users.findById(userId)).thenReturn(Optional.of(user(UserStatus.ACTIVE)));
        when(memberships.findActive(userId, tenantId)).thenReturn(Optional.empty());
        assertError(() -> service.select(command()), TenantErrorCodes.ACCESS_DENIED);

        TenantMembership membership = TenantMembership.owner(tenantId, userId, NOW);
        when(memberships.findActive(userId, tenantId)).thenReturn(Optional.of(membership));
        when(tenants.findById(tenantId)).thenReturn(Optional.of(tenant(TenantStatus.INACTIVE)));
        assertError(() -> service.select(command()), TenantErrorCodes.ACCESS_DENIED);
        verifyNoInteractions(accessTokens);
    }

    private SelectTenantUseCase.SelectTenantCommand command() {
        return new SelectTenantUseCase.SelectTenantCommand("selection-token", tenantId);
    }

    private User user(UserStatus status) {
        return new User(
                userId,
                "owner@example.com",
                "hash",
                "Owner",
                null,
                status,
                null,
                NOW,
                NOW,
                null);
    }

    private Tenant tenant(TenantStatus status) {
        return new Tenant(tenantId, "TENANT", "Tenant", status, NOW, NOW, null);
    }

    private static void assertError(Runnable action, String code) {
        assertThatExceptionOfType(StableApiException.class)
                .isThrownBy(action::run)
                .satisfies(exception -> assertThat(exception.code()).isEqualTo(code));
    }
}
