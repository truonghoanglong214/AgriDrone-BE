package com.agridrone.be1.shared.auth;

import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.security.oauth2.core.OAuth2Error;
import org.springframework.security.oauth2.core.OAuth2TokenValidator;
import org.springframework.security.oauth2.core.OAuth2TokenValidatorResult;
import org.springframework.security.oauth2.core.DelegatingOAuth2TokenValidator;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtClaimValidator;
import org.springframework.security.oauth2.jwt.JwtValidators;
import org.springframework.security.oauth2.jwt.NimbusJwtDecoder;
import org.springframework.security.oauth2.jose.jws.SignatureAlgorithm;
import org.springframework.util.StringUtils;
import java.time.Instant;
import java.util.List;
import java.util.UUID;

@Configuration(proxyBeanMethods = false)
@ConditionalOnProperty(prefix = "agridrone.security.jwt", name = "enabled", havingValue = "true")
public class JwtDecoderConfiguration {

    @Bean
    JwtDecoder jwtDecoder(JwtSecurityProperties properties) {
        if (!StringUtils.hasText(properties.jwkSetUri())) {
            throw new IllegalStateException(
                    "BE1_JWT_JWK_SET_URI is required when JWT verification is enabled.");
        }
        if (!StringUtils.hasText(properties.issuer())) {
            throw new IllegalStateException(
                    "BE1_JWT_ISSUER is required when JWT verification is enabled.");
        }
        if (!StringUtils.hasText(properties.audience())) {
            throw new IllegalStateException(
                    "BE1_JWT_AUDIENCE is required when JWT verification is enabled.");
        }

        NimbusJwtDecoder decoder = NimbusJwtDecoder.withJwkSetUri(properties.jwkSetUri())
                .jwsAlgorithm(SignatureAlgorithm.RS256)
                .build();
        decoder.setJwtValidator(jwtValidator(properties));
        return decoder;
    }

    OAuth2TokenValidator<Jwt> jwtValidator(JwtSecurityProperties properties) {
        return new DelegatingOAuth2TokenValidator<>(
                JwtValidators.createDefaultWithIssuer(properties.issuer()),
                new JwtClaimValidator<List<String>>("aud",
                        audience -> audience != null && audience.contains(properties.audience())),
                new JwtClaimValidator<Instant>("exp", value -> value != null),
                new JwtClaimValidator<Instant>("nbf", value -> value != null),
                new JwtClaimValidator<String>("sub", value -> isUuid(value)),
                this::validateRsaHeader);
    }

    private OAuth2TokenValidatorResult validateRsaHeader(Jwt jwt) {
        Object algorithm = jwt.getHeaders().get("alg");
        Object keyId = jwt.getHeaders().get("kid");
        if (!SignatureAlgorithm.RS256.getName().equals(algorithm)
                || !StringUtils.hasText(keyId == null ? null : keyId.toString())) {
            return OAuth2TokenValidatorResult.failure(new OAuth2Error(
                    "invalid_token",
                    "JWT must use RS256 and contain a non-empty kid header.",
                    null));
        }
        return OAuth2TokenValidatorResult.success();
    }

    private static boolean isUuid(String value) {
        if (!StringUtils.hasText(value)) {
            return false;
        }
        try {
            UUID.fromString(value);
            return true;
        } catch (IllegalArgumentException exception) {
            return false;
        }
    }
}
