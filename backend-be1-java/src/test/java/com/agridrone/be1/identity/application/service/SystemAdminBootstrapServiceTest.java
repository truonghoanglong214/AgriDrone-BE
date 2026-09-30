package com.agridrone.be1.identity.application.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.agridrone.be1.identity.application.error.BootstrapErrorCodes;
import com.agridrone.be1.identity.application.exception.BootstrapException;
import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminCommand;
import com.agridrone.be1.identity.application.port.out.initialization.SystemAdminBootstrapLock;
import com.agridrone.be1.identity.application.port.out.persistence.RoleRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import com.agridrone.be1.identity.domain.SystemRole;
import com.agridrone.be1.identity.domain.SystemRoleCodes;
import com.agridrone.be1.identity.domain.User;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

@ExtendWith(MockitoExtension.class)
class SystemAdminBootstrapServiceTest {
    private static final Instant NOW = Instant.parse("2026-09-30T07:00:00Z");
    private static final UUID ROLE_ID = UUID.fromString(
            "00000000-0000-0000-0000-000000000001");

    @Mock
    RoleRepository roles;

    @Mock
    UserRepository users;

    @Mock
    SystemAdminBootstrapLock bootstrapLock;

    @Mock
    PasswordHasher passwordHasher;

    SystemAdminBootstrapService service;

    @BeforeEach
    void setUp() {
        service = new SystemAdminBootstrapService(
                roles,
                users,
                bootstrapLock,
                passwordHasher,
                Clock.fixed(NOW, ZoneOffset.UTC));
    }

    @Test
    void createsInitialAdminWithSeededRole() {
        when(roles.existsActiveUserWithRole(SystemRoleCodes.SYSTEM_ADMIN)).thenReturn(false);
        when(roles.findByCode(SystemRoleCodes.SYSTEM_ADMIN))
                .thenReturn(Optional.of(new SystemRole(ROLE_ID, SystemRoleCodes.SYSTEM_ADMIN)));
        when(users.findByEmailIncludingDeleted("admin@example.com"))
                .thenReturn(Optional.empty());
        when(passwordHasher.hash(anyString())).thenReturn("bootstrap-password-hash");

        var result = service.bootstrap(new BootstrapSystemAdminCommand(
                "  ADMIN@Example.COM ",
                "  System Administrator  "));

        assertThat(result.created()).isTrue();
        assertThat(result.email()).isEqualTo("admin@example.com");
        assertThat(result.userId()).isNotNull();

        ArgumentCaptor<User> userCaptor = ArgumentCaptor.forClass(User.class);
        verify(users).save(userCaptor.capture());
        User user = userCaptor.getValue();
        assertThat(user.email()).isEqualTo("admin@example.com");
        assertThat(user.fullName()).isEqualTo("System Administrator");
        assertThat(user.phone()).isNull();
        assertThat(user.createdAt()).isEqualTo(NOW);
        assertThat(user.passwordHash()).isEqualTo("bootstrap-password-hash");
        ArgumentCaptor<String> passwordCaptor = ArgumentCaptor.forClass(String.class);
        verify(passwordHasher).hash(passwordCaptor.capture());
        assertThat(passwordCaptor.getValue()).hasSize(64);
        verify(users).assignSystemRole(user.id(), ROLE_ID);
        verify(bootstrapLock).acquire();
    }

    @Test
    void returnsNoOpWhenAnActiveAdminAlreadyExists() {
        when(roles.existsActiveUserWithRole(SystemRoleCodes.SYSTEM_ADMIN)).thenReturn(true);

        var result = service.bootstrap(new BootstrapSystemAdminCommand(
                "admin@example.com",
                "System Administrator"));

        assertThat(result.created()).isFalse();
        assertThat(result.userId()).isNull();
        verify(users, never()).save(org.mockito.ArgumentMatchers.any());
        verify(roles, never()).findByCode(anyString());
    }

    @Test
    void failsWithStableCodeWhenSeededRoleIsMissing() {
        when(roles.existsActiveUserWithRole(SystemRoleCodes.SYSTEM_ADMIN)).thenReturn(false);
        when(roles.findByCode(SystemRoleCodes.SYSTEM_ADMIN)).thenReturn(Optional.empty());

        assertThatThrownBy(() -> service.bootstrap(new BootstrapSystemAdminCommand(
                "admin@example.com",
                "System Administrator")))
                .isInstanceOfSatisfying(BootstrapException.class, exception ->
                        assertThat(exception.getCode())
                                .isEqualTo(BootstrapErrorCodes.SYSTEM_ADMIN_ROLE_MISSING));

        verify(users, never()).save(org.mockito.ArgumentMatchers.any());
    }

    @Test
    void doesNotPromoteAnExistingUser() {
        when(roles.existsActiveUserWithRole(SystemRoleCodes.SYSTEM_ADMIN)).thenReturn(false);
        when(roles.findByCode(SystemRoleCodes.SYSTEM_ADMIN))
                .thenReturn(Optional.of(new SystemRole(ROLE_ID, SystemRoleCodes.SYSTEM_ADMIN)));
        when(users.findByEmailIncludingDeleted("admin@example.com"))
                .thenReturn(Optional.of(User.create(
                        "admin@example.com",
                        "existing-hash",
                        "Existing User",
                        null,
                        NOW)));

        assertThatThrownBy(() -> service.bootstrap(new BootstrapSystemAdminCommand(
                "admin@example.com",
                "System Administrator")))
                .isInstanceOfSatisfying(BootstrapException.class, exception ->
                        assertThat(exception.getCode())
                                .isEqualTo(BootstrapErrorCodes.CONFIGURED_EMAIL_ALREADY_EXISTS));

        verify(users, never()).save(org.mockito.ArgumentMatchers.any());
        verify(users, never()).assignSystemRole(
                org.mockito.ArgumentMatchers.any(),
                org.mockito.ArgumentMatchers.any());
    }
}
