package com.agridrone.be1.identity.infrastructure.security;

import static org.assertj.core.api.Assertions.assertThat;

import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import org.junit.jupiter.api.Test;
import org.springframework.security.crypto.password.PasswordEncoder;

class BCryptPasswordHasherTest {

    private final PasswordEncoder encoder =
            new PasswordSecurityConfiguration().passwordEncoder();
    private final PasswordHasher hasher = new BCryptPasswordHasher(encoder);

    @Test
    void hashesAndVerifiesPassword() {
        String password = "ValidPassword123!";

        String hash = hasher.hash(password);

        assertThat(hash).isNotEqualTo(password);
        assertThat(hash).startsWith("$2a$11$");
        assertThat(hasher.matches(password, hash)).isTrue();
        assertThat(hasher.matches("WrongPassword123!", hash)).isFalse();
    }

    @Test
    void usesRandomSaltForEveryHash() {
        String password = "ValidPassword123!";

        String firstHash = hasher.hash(password);
        String secondHash = hasher.hash(password);

        assertThat(firstHash).isNotEqualTo(secondHash);
        assertThat(hasher.matches(password, firstHash)).isTrue();
        assertThat(hasher.matches(password, secondHash)).isTrue();
    }
}
