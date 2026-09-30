package com.agridrone.be1.identity.application.service;

import com.agridrone.be1.identity.application.error.BootstrapErrorCodes;
import com.agridrone.be1.identity.application.exception.BootstrapException;
import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminCommand;
import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminResult;
import com.agridrone.be1.identity.application.port.in.bootstrapsystemadmin.BootstrapSystemAdminUseCase;
import com.agridrone.be1.identity.application.port.out.initialization.SystemAdminBootstrapLock;
import com.agridrone.be1.identity.application.port.out.persistence.RoleRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import com.agridrone.be1.identity.domain.SystemRole;
import com.agridrone.be1.identity.domain.SystemRoleCodes;
import com.agridrone.be1.identity.domain.User;
import java.security.SecureRandom;
import java.time.Clock;
import java.time.Instant;
import java.util.Base64;
import java.util.Locale;
import java.util.Objects;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class SystemAdminBootstrapService implements BootstrapSystemAdminUseCase {
    // BCrypt accepts at most 72 UTF-8 bytes. URL-safe Base64 of 48 random
    // bytes is exactly 64 ASCII characters and remains below that limit.
    private static final int GENERATED_PASSWORD_BYTES = 48;
    private static final SecureRandom SECURE_RANDOM = new SecureRandom();

    private final RoleRepository roles;
    private final UserRepository users;
    private final SystemAdminBootstrapLock bootstrapLock;
    private final PasswordHasher passwordHasher;
    private final Clock clock;

    public SystemAdminBootstrapService(
            RoleRepository roles,
            UserRepository users,
            SystemAdminBootstrapLock bootstrapLock,
            PasswordHasher passwordHasher,
            Clock clock) {
        this.roles = roles;
        this.users = users;
        this.bootstrapLock = bootstrapLock;
        this.passwordHasher = passwordHasher;
        this.clock = clock;
    }

    @Override
    @Transactional
    public BootstrapSystemAdminResult bootstrap(BootstrapSystemAdminCommand command) {
        Objects.requireNonNull(command, "command");
        String normalizedEmail = normalizeRequired(command.email(), "email")
                .toLowerCase(Locale.ROOT);
        String normalizedFullName = normalizeRequired(command.fullName(), "fullName");

        // The pessimistic row lock remains held until this transaction completes.
        // A concurrent application instance must re-check the state afterwards.
        bootstrapLock.acquire();

        if (roles.existsActiveUserWithRole(SystemRoleCodes.SYSTEM_ADMIN)) {
            return new BootstrapSystemAdminResult(false, null, normalizedEmail);
        }

        SystemRole systemAdminRole = roles.findByCode(SystemRoleCodes.SYSTEM_ADMIN)
                .orElseThrow(() -> new BootstrapException(
                        BootstrapErrorCodes.SYSTEM_ADMIN_ROLE_MISSING));

        if (users.findByEmailIncludingDeleted(normalizedEmail).isPresent()) {
            throw new BootstrapException(
                    BootstrapErrorCodes.CONFIGURED_EMAIL_ALREADY_EXISTS);
        }

        Instant now = clock.instant();
        User user = User.create(
                normalizedEmail,
                passwordHasher.hash(generatePassword()),
                normalizedFullName,
                null,
                now);

        users.save(user);
        users.assignSystemRole(user.id(), systemAdminRole.id());

        return new BootstrapSystemAdminResult(true, user.id(), user.email());
    }

    private static String generatePassword() {
        byte[] passwordBytes = new byte[GENERATED_PASSWORD_BYTES];
        SECURE_RANDOM.nextBytes(passwordBytes);
        return Base64.getUrlEncoder().withoutPadding().encodeToString(passwordBytes);
    }

    private static String normalizeRequired(String value, String field) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException(field + " is required");
        }
        return value.trim();
    }
}
