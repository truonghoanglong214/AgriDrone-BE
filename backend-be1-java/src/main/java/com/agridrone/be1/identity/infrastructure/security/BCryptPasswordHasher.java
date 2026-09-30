package com.agridrone.be1.identity.infrastructure.security;

import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Component;

@Component
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class BCryptPasswordHasher implements PasswordHasher {
    private final PasswordEncoder encoder;

    public BCryptPasswordHasher(PasswordEncoder encoder) {
        this.encoder = encoder;
    }

    @Override
    public String hash(String plainTextPassword) {
        return encoder.encode(plainTextPassword);
    }

    @Override
    public boolean matches(String plainTextPassword, String passwordHash) {
        return encoder.matches(plainTextPassword, passwordHash);
    }
}
