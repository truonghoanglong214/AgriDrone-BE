package com.agridrone.be1.shared.auth;

import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.assertThat;

import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.security.oauth2.jwt.Jwt;

class JwtDecoderConfigurationTest {

    @Test
    void enabledJwtRequiresExternalJwkSetLocation() {
        JwtDecoderConfiguration configuration = new JwtDecoderConfiguration();
        JwtSecurityProperties properties = new JwtSecurityProperties(true, "", "", "");

        assertThatThrownBy(() -> configuration.jwtDecoder(properties))
                .isInstanceOf(IllegalStateException.class)
                .hasMessageContaining("BE1_JWT_JWK_SET_URI");
    }

    @Test
    void validatorRequiresIssuerAudienceTemporalClaimsUuidSubjectAndRs256KeyId() {
        JwtDecoderConfiguration configuration = new JwtDecoderConfiguration();
        JwtSecurityProperties properties = new JwtSecurityProperties(
                true, "https://jwt.test/jwks", "test-issuer", "test-audience");
        Instant now = Instant.now().minusSeconds(30);
        Jwt valid = Jwt.withTokenValue("token")
                .header("alg", "RS256")
                .header("kid", "key-1")
                .issuer("test-issuer")
                .audience(List.of("test-audience"))
                .subject(UUID.randomUUID().toString())
                .issuedAt(now)
                .notBefore(now)
                .expiresAt(now.plusSeconds(300))
                .build();

        assertThat(configuration.jwtValidator(properties).validate(valid).hasErrors()).isFalse();
    }

    @Test
    void validatorRejectsWrongAudienceAndMissingRequiredClaims() {
        JwtDecoderConfiguration configuration = new JwtDecoderConfiguration();
        JwtSecurityProperties properties = new JwtSecurityProperties(
                true, "https://jwt.test/jwks", "test-issuer", "test-audience");
        Instant now = Instant.now().minusSeconds(30);
        Jwt wrongAudience = Jwt.withTokenValue("token")
                .header("alg", "RS256")
                .header("kid", "key-1")
                .issuer("test-issuer")
                .audience(List.of("other-audience"))
                .subject(UUID.randomUUID().toString())
                .issuedAt(now)
                .notBefore(now)
                .expiresAt(now.plusSeconds(300))
                .build();
        Jwt missingNotBefore = Jwt.withTokenValue("token")
                .header("alg", "RS256")
                .header("kid", "key-1")
                .issuer("test-issuer")
                .audience(List.of("test-audience"))
                .subject(UUID.randomUUID().toString())
                .issuedAt(now)
                .expiresAt(now.plusSeconds(300))
                .build();

        assertThat(configuration.jwtValidator(properties).validate(wrongAudience).hasErrors()).isTrue();
        assertThat(configuration.jwtValidator(properties).validate(missingNotBefore).hasErrors()).isTrue();
    }

    @Test
    void validatorRejectsNonRsaAlgorithmMissingKeyIdAndNonUuidSubject() {
        JwtDecoderConfiguration configuration = new JwtDecoderConfiguration();
        JwtSecurityProperties properties = new JwtSecurityProperties(
                true, "https://jwt.test/jwks", "test-issuer", "test-audience");
        Instant now = Instant.now().minusSeconds(30);

        Jwt wrongAlgorithm = Jwt.withTokenValue("token")
                .header("alg", "HS256")
                .header("kid", "key-1")
                .issuer("test-issuer")
                .audience(List.of("test-audience"))
                .subject(UUID.randomUUID().toString())
                .issuedAt(now)
                .notBefore(now)
                .expiresAt(now.plusSeconds(300))
                .build();
        Jwt missingKeyId = Jwt.withTokenValue("token")
                .header("alg", "RS256")
                .issuer("test-issuer")
                .audience(List.of("test-audience"))
                .subject(UUID.randomUUID().toString())
                .issuedAt(now)
                .notBefore(now)
                .expiresAt(now.plusSeconds(300))
                .build();
        Jwt nonUuidSubject = Jwt.withTokenValue("token")
                .header("alg", "RS256")
                .header("kid", "key-1")
                .issuer("test-issuer")
                .audience(List.of("test-audience"))
                .subject("not-a-uuid")
                .issuedAt(now)
                .notBefore(now)
                .expiresAt(now.plusSeconds(300))
                .build();

        assertThat(configuration.jwtValidator(properties).validate(wrongAlgorithm).hasErrors()).isTrue();
        assertThat(configuration.jwtValidator(properties).validate(missingKeyId).hasErrors()).isTrue();
        assertThat(configuration.jwtValidator(properties).validate(nonUuidSubject).hasErrors()).isTrue();
    }
}
