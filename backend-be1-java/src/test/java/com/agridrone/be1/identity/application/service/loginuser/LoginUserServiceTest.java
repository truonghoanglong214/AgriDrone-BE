package com.agridrone.be1.identity.application.service.loginuser;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatExceptionOfType;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import com.agridrone.be1.identity.application.error.AuthenticationErrorCodes;
import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserCommand;
import com.agridrone.be1.identity.application.port.out.persistence.TenantMembershipRepository;
import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.AccessTokenIssuer;
import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import com.agridrone.be1.identity.application.port.out.security.TenantSelectionTokenService;
import com.agridrone.be1.identity.application.security.IssuedAccessToken;
import com.agridrone.be1.identity.application.security.IssuedTenantSelectionToken;
import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.domain.TenantMembership;
import com.agridrone.be1.identity.domain.TenantStatus;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.shared.error.StableApiException;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.List;
import java.util.Optional;
import java.util.Set;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class LoginUserServiceTest {
    private static final Instant NOW = Instant.parse("2026-10-01T00:00:00Z");

    @ParameterizedTest
    @ValueSource(strings = {"SYSTEM_ADMIN", "SYSTEM_MANAGER"})
    void issuesSystemSessionForPrivilegedUserAndRecordsLogin(String systemRole) {
        Fixture fixture = new Fixture();
        when(fixture.users.findSystemRoleCodes(fixture.user.id())).thenReturn(Set.of(systemRole));
        when(fixture.accessTokens.issue(
                        fixture.user.id(), null, null, null, List.of(systemRole)))
                .thenReturn(new IssuedAccessToken("token", NOW, NOW.plusSeconds(900)));

        var result = fixture.service().login(fixture.command());

        assertThat(result.session().accessToken()).isEqualTo("token");
        assertThat(result.session().tenant()).isNull();
        assertThat(result.tenantSelection()).isNull();
        assertThat(fixture.user.lastLoginAt()).isEqualTo(NOW);
        verify(fixture.users).save(fixture.user);
        verifyNoInteractions(fixture.memberships, fixture.selectionTokens);
    }

    @Test
    void rejectsUserWithoutAnyActiveTenantAndDoesNotRecordLogin() {
        Fixture fixture = new Fixture();
        when(fixture.memberships.findActiveByUserId(fixture.user.id())).thenReturn(List.of());

        assertThatExceptionOfType(StableApiException.class)
                .isThrownBy(() -> fixture.service().login(fixture.command()))
                .satisfies(exception -> {
                    assertThat(exception.status().value()).isEqualTo(403);
                    assertThat(exception.code()).isEqualTo(
                            AuthenticationErrorCodes.NO_TENANT_MEMBERSHIP);
                });

        assertThat(fixture.user.lastLoginAt()).isNull();
        verify(fixture.users, never()).save(fixture.user);
        verifyNoInteractions(fixture.accessTokens, fixture.selectionTokens);
    }

    @Test
    void issuesTenantSessionWhenOnlyOneActiveTenantRemainsAfterFiltering() {
        Fixture fixture = new Fixture();
        Tenant active = tenant("ACTIVE", "Active Tenant", TenantStatus.ACTIVE);
        Tenant inactive = tenant("INACTIVE", "Inactive Tenant", TenantStatus.INACTIVE);
        TenantMembership activeMembership = membership(fixture.user, active);
        TenantMembership staleMembership = membership(fixture.user, inactive);
        when(fixture.memberships.findActiveByUserId(fixture.user.id()))
                .thenReturn(List.of(staleMembership, activeMembership));
        when(fixture.tenants.findById(inactive.id())).thenReturn(Optional.of(inactive));
        when(fixture.tenants.findById(active.id())).thenReturn(Optional.of(active));
        when(fixture.accessTokens.issue(
                        fixture.user.id(),
                        active.id(),
                        activeMembership.id(),
                        "OWNER",
                        List.of()))
                .thenReturn(new IssuedAccessToken("access-token", NOW, NOW.plusSeconds(900)));

        var result = fixture.service().login(fixture.command());

        assertThat(result.session().accessToken()).isEqualTo("access-token");
        assertThat(result.session().tenant().id()).isEqualTo(active.id());
        assertThat(result.tenantSelection()).isNull();
        verifyNoInteractions(fixture.selectionTokens);
        verify(fixture.users).save(fixture.user);
    }

    @Test
    void returnsSelectionTokenAndSortedActiveTenantsWithoutIssuingAccessToken() {
        Fixture fixture = new Fixture();
        Tenant zulu = tenant("ZULU", "Zulu Farm", TenantStatus.ACTIVE);
        Tenant alpha = tenant("ALPHA", "Alpha Farm", TenantStatus.ACTIVE);
        Tenant inactive = tenant("OLD", "Old Farm", TenantStatus.INACTIVE);
        TenantMembership zuluMembership = membership(fixture.user, zulu);
        TenantMembership inactiveMembership = membership(fixture.user, inactive);
        TenantMembership alphaMembership = membership(fixture.user, alpha);
        when(fixture.memberships.findActiveByUserId(fixture.user.id()))
                .thenReturn(List.of(zuluMembership, inactiveMembership, alphaMembership));
        when(fixture.tenants.findById(zulu.id())).thenReturn(Optional.of(zulu));
        when(fixture.tenants.findById(alpha.id())).thenReturn(Optional.of(alpha));
        when(fixture.tenants.findById(inactive.id())).thenReturn(Optional.of(inactive));
        when(fixture.selectionTokens.issue(fixture.user.id()))
                .thenReturn(new IssuedTenantSelectionToken(
                        "selection-token", NOW, NOW.plusSeconds(300)));

        var result = fixture.service().login(fixture.command());

        assertThat(result.session()).isNull();
        assertThat(result.tenantSelection().selectionToken()).isEqualTo("selection-token");
        assertThat(result.tenantSelection().expiresAt()).isEqualTo(NOW.plusSeconds(300));
        assertThat(result.tenantSelection().tenants())
                .extracting(tenant -> tenant.name())
                .containsExactly("Alpha Farm", "Zulu Farm");
        verifyNoInteractions(fixture.accessTokens);
        verify(fixture.users).save(fixture.user);
        assertThat(fixture.user.lastLoginAt()).isEqualTo(NOW);
    }

    @Test
    void doesNotRecordLoginWhenSelectionTokenIssuingFails() {
        Fixture fixture = new Fixture();
        Tenant first = tenant("ONE", "One", TenantStatus.ACTIVE);
        Tenant second = tenant("TWO", "Two", TenantStatus.ACTIVE);
        when(fixture.memberships.findActiveByUserId(fixture.user.id()))
                .thenReturn(List.of(
                        membership(fixture.user, first),
                        membership(fixture.user, second)));
        when(fixture.tenants.findById(first.id())).thenReturn(Optional.of(first));
        when(fixture.tenants.findById(second.id())).thenReturn(Optional.of(second));
        when(fixture.selectionTokens.issue(fixture.user.id()))
                .thenThrow(new IllegalStateException("issuer unavailable"));

        assertThatExceptionOfType(IllegalStateException.class)
                .isThrownBy(() -> fixture.service().login(fixture.command()));

        assertThat(fixture.user.lastLoginAt()).isNull();
        verify(fixture.users, never()).save(fixture.user);
    }

    private static Tenant tenant(String code, String name, TenantStatus status) {
        return new Tenant(
                UUID.randomUUID(), code, name, status,
                NOW.minusSeconds(60), NOW.minusSeconds(60), null);
    }

    private static TenantMembership membership(User user, Tenant tenant) {
        return new TenantMembership(
                UUID.randomUUID(), tenant.id(), user.id(), "OWNER", "ACTIVE",
                NOW.minusSeconds(60), NOW.minusSeconds(60), 1);
    }

    private static final class Fixture {
        private final UserRepository users = org.mockito.Mockito.mock(UserRepository.class);
        private final TenantMembershipRepository memberships =
                org.mockito.Mockito.mock(TenantMembershipRepository.class);
        private final TenantRepository tenants = org.mockito.Mockito.mock(TenantRepository.class);
        private final PasswordHasher hasher = org.mockito.Mockito.mock(PasswordHasher.class);
        private final AccessTokenIssuer accessTokens =
                org.mockito.Mockito.mock(AccessTokenIssuer.class);
        private final TenantSelectionTokenService selectionTokens =
                org.mockito.Mockito.mock(TenantSelectionTokenService.class);
        private final User user = User.create(
                "admin@example.com", "hash", "Admin", null, NOW.minusSeconds(10));

        private Fixture() {
            when(users.findByEmail("admin@example.com")).thenReturn(Optional.of(user));
            when(hasher.matches("Password123!", "hash")).thenReturn(true);
            when(users.findSystemRoleCodes(user.id())).thenReturn(Set.of());
        }

        private LoginUserService service() {
            return new LoginUserService(
                    users,
                    memberships,
                    tenants,
                    hasher,
                    accessTokens,
                    selectionTokens,
                    Clock.fixed(NOW, ZoneOffset.UTC));
        }

        private LoginUserCommand command() {
            return new LoginUserCommand(" Admin@Example.com ", "Password123!");
        }
    }
}
