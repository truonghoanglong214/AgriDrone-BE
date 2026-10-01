package com.agridrone.be1.shared.auth;

import static org.assertj.core.api.Assertions.assertThat;

import java.util.List;
import java.util.stream.Collectors;
import org.junit.jupiter.api.Test;
import org.springframework.security.oauth2.jwt.Jwt;

class JwtAuthenticationConverterTest {

    @Test
    void mapsSystemRoleClaimWithoutScopePrefix() {
        Jwt jwt = Jwt.withTokenValue("token")
                .header("alg", "RS256")
                .claim("system_role", List.of("SYSTEM_ADMIN", "SYSTEM_MANAGER"))
                .claim("scope", "read")
                .build();

        var authorities = new JwtAuthoritiesConverter().convert(jwt).stream()
                .map(Object::toString)
                .collect(Collectors.toSet());

        assertThat(authorities)
                .contains("SYSTEM_ADMIN", "SYSTEM_MANAGER", "SCOPE_read");
    }

    @Test
    void supportsSingleSystemRoleClaim() {
        Jwt jwt = Jwt.withTokenValue("token")
                .header("alg", "RS256")
                .claim("system_role", "SYSTEM_ADMIN")
                .build();

        assertThat(new JwtAuthoritiesConverter().convert(jwt).stream()
                .map(Object::toString))
                .containsExactly("SYSTEM_ADMIN");
    }
}
