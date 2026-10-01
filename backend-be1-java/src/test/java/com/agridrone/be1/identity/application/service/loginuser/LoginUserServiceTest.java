package com.agridrone.be1.identity.application.service.loginuser;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.agridrone.be1.identity.application.port.out.persistence.TenantMembershipRepository;
import com.agridrone.be1.identity.application.port.out.persistence.TenantRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.AccessTokenIssuer;
import com.agridrone.be1.identity.application.security.IssuedAccessToken;
import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import com.agridrone.be1.identity.domain.User;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.Optional;
import java.util.Set;
import org.junit.jupiter.api.Test;

class LoginUserServiceTest {
    private static final Instant NOW = Instant.parse("2026-10-01T00:00:00Z");

    @Test
    void issuesSystemAdminTokenAndRecordsLogin() {
        UserRepository users = org.mockito.Mockito.mock(UserRepository.class);
        TenantMembershipRepository memberships = org.mockito.Mockito.mock(TenantMembershipRepository.class);
        TenantRepository tenants = org.mockito.Mockito.mock(TenantRepository.class);
        PasswordHasher hasher = org.mockito.Mockito.mock(PasswordHasher.class);
        AccessTokenIssuer issuer = org.mockito.Mockito.mock(AccessTokenIssuer.class);
        User user = User.create("admin@example.com", "hash", "Admin", null, NOW.minusSeconds(10));
        when(users.findByEmail("admin@example.com")).thenReturn(Optional.of(user));
        when(hasher.matches("Password123!", "hash")).thenReturn(true);
        when(users.findSystemRoleCodes(user.id())).thenReturn(Set.of("SYSTEM_ADMIN"));
        when(issuer.issue(user.id(), null, null, null, java.util.List.of("SYSTEM_ADMIN")))
                .thenReturn(new IssuedAccessToken(
                        "token", NOW, NOW.plusSeconds(900)));
        LoginUserService service = new LoginUserService(
                users, memberships, tenants, hasher, issuer, Clock.fixed(NOW, ZoneOffset.UTC));

        var result = service.login(new com.agridrone.be1.identity.application.port.in.loginuser.LoginUserCommand(
                " Admin@Example.com ", "Password123!"));

        assertThat(result.session().accessToken()).isEqualTo("token");
        assertThat(result.session().tenant()).isNull();
        assertThat(user.lastLoginAt()).isEqualTo(NOW);
        verify(users).save(user);
    }
}
