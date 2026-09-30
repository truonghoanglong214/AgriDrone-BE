package com.agridrone.be1.shared.auth;

import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.security.oauth2.jwt.JwtValidators;
import org.springframework.security.oauth2.jwt.NimbusJwtDecoder;
import org.springframework.util.StringUtils;

@Configuration(proxyBeanMethods = false)
@ConditionalOnProperty(prefix = "agridrone.security.jwt", name = "enabled", havingValue = "true")
public class JwtDecoderConfiguration {

    @Bean
    JwtDecoder jwtDecoder(JwtSecurityProperties properties) {
        if (!StringUtils.hasText(properties.jwkSetUri())) {
            throw new IllegalStateException(
                    "BE1_JWT_JWK_SET_URI is required when JWT verification is enabled.");
        }

        NimbusJwtDecoder decoder = NimbusJwtDecoder.withJwkSetUri(properties.jwkSetUri()).build();
        if (StringUtils.hasText(properties.issuer())) {
            decoder.setJwtValidator(JwtValidators.createDefaultWithIssuer(properties.issuer()));
        }
        return decoder;
    }
}
