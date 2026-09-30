package com.agridrone.be1.shared.auth;

import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.Test;

class JwtDecoderConfigurationTest {

    @Test
    void enabledJwtRequiresExternalJwkSetLocation() {
        JwtDecoderConfiguration configuration = new JwtDecoderConfiguration();
        JwtSecurityProperties properties = new JwtSecurityProperties(true, "", "");

        assertThatThrownBy(() -> configuration.jwtDecoder(properties))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("BE1_JWT_JWK_SET_URI");
    }
}
