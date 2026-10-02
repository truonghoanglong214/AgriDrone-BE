package com.agridrone.be1.shared.auth;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatExceptionOfType;

import com.agridrone.be1.identity.infrastructure.security.jwt.JwtTenantSelectionTokenService;
import com.nimbusds.jose.jwk.JWKSet;
import com.nimbusds.jose.jwk.RSAKey;
import com.nimbusds.jose.jwk.source.ImmutableJWKSet;
import java.security.KeyPair;
import java.security.KeyPairGenerator;
import java.security.interfaces.RSAPrivateKey;
import java.security.interfaces.RSAPublicKey;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.security.oauth2.core.OAuth2TokenValidatorResult;
import org.springframework.security.oauth2.jose.jws.SignatureAlgorithm;
import org.springframework.security.oauth2.jwt.JwtClaimsSet;
import org.springframework.security.oauth2.jwt.JwtEncoder;
import org.springframework.security.oauth2.jwt.JwtEncoderParameters;
import org.springframework.security.oauth2.jwt.JwtException;
import org.springframework.security.oauth2.jwt.JwsHeader;
import org.springframework.security.oauth2.jwt.NimbusJwtDecoder;
import org.springframework.security.oauth2.jwt.NimbusJwtEncoder;

class TenantSelectionTokenServiceTest {
    private static final Instant NOW = Instant.parse("2026-10-02T00:00:00Z");
    private static final UUID USER_ID =
            UUID.fromString("11111111-1111-1111-1111-111111111111");
    private static final UUID TOKEN_ID =
            UUID.fromString("22222222-2222-2222-2222-222222222222");
    private static final String ISSUER = "https://auth.agridrone.test";
    private static final String AUDIENCE = "agridrone-api";
    private static final String KEY_ID = "selection-test-key";

    private RSAKey rsaKey;
    private JwtEncoder encoder;
    private JwtIssuerProperties properties;
    private JwtTenantSelectionTokenService service;

    @BeforeEach
    void setUp() throws Exception {
        rsaKey = rsaKey(KEY_ID);
        encoder = encoder(rsaKey);
        properties = new JwtIssuerProperties(
                true,
                ISSUER,
                AUDIENCE,
                "unused-private-key",
                "unused-public-key",
                KEY_ID,
                Duration.ofMinutes(15),
                Duration.ofMinutes(5));
        service = new JwtTenantSelectionTokenService(
                encoder,
                rsaKey,
                properties,
                Clock.fixed(NOW, ZoneOffset.UTC),
                () -> TOKEN_ID);
    }

    @Test
    void issuesRs256SelectionTokenWithRequiredClaimsAndValidatesSubject() throws Exception {
        var issued = service.issue(USER_ID);

        assertThat(issued.issuedAt()).isEqualTo(NOW);
        assertThat(issued.expiresAt()).isEqualTo(NOW.plus(Duration.ofMinutes(5)));
        assertThat(service.validate(issued.value())).contains(USER_ID);

        var decoder = NimbusJwtDecoder.withPublicKey(rsaKey.toRSAPublicKey()).build();
        decoder.setJwtValidator(jwt -> OAuth2TokenValidatorResult.success());
        var jwt = decoder.decode(issued.value());
        assertThat(jwt.getHeaders())
                .containsEntry("alg", "RS256")
                .containsEntry("kid", KEY_ID);
        assertThat(jwt.getIssuer()).hasToString(ISSUER);
        assertThat(jwt.getAudience()).containsExactly(AUDIENCE + ".TenantSelection");
        assertThat(jwt.getSubject()).isEqualTo(USER_ID.toString());
        assertThat(jwt.getId()).isEqualTo(TOKEN_ID.toString());
        assertThat(jwt.getIssuedAt()).isEqualTo(NOW);
        assertThat(jwt.getNotBefore()).isEqualTo(NOW);
        assertThat(jwt.getExpiresAt()).isEqualTo(NOW.plus(Duration.ofMinutes(5)));
        assertThat(jwt.getClaimAsString("purpose")).isEqualTo("tenant_selection");
    }

    @Test
    void rejectsMalformedOrWronglySignedSelectionToken() throws Exception {
        RSAKey otherKey = rsaKey(KEY_ID);
        String wronglySigned = token(
                encoder(otherKey),
                ISSUER,
                List.of(AUDIENCE + ".TenantSelection"),
                USER_ID.toString(),
                "tenant_selection",
                NOW,
                NOW.plusSeconds(300));

        assertThat(service.validate(null)).isEmpty();
        assertThat(service.validate(" ")).isEmpty();
        assertThat(service.validate("not-a-jwt")).isEmpty();
        assertThat(service.validate(wronglySigned)).isEmpty();
    }

    @Test
    void rejectsWrongIssuerAudiencePurposeSubjectAndExpiry() {
        assertRejected(token(
                encoder, "https://wrong-issuer.test",
                List.of(AUDIENCE + ".TenantSelection"), USER_ID.toString(),
                "tenant_selection", NOW, NOW.plusSeconds(300)));
        assertRejected(token(
                encoder, ISSUER,
                List.of(AUDIENCE), USER_ID.toString(),
                "tenant_selection", NOW, NOW.plusSeconds(300)));
        assertRejected(token(
                encoder, ISSUER,
                List.of(AUDIENCE + ".TenantSelection"), USER_ID.toString(),
                "access", NOW, NOW.plusSeconds(300)));
        assertRejected(token(
                encoder, ISSUER,
                List.of(AUDIENCE + ".TenantSelection"), null,
                "tenant_selection", NOW, NOW.plusSeconds(300)));
        assertRejected(token(
                encoder, ISSUER,
                List.of(AUDIENCE + ".TenantSelection"), "not-a-uuid",
                "tenant_selection", NOW, NOW.plusSeconds(300)));
        assertRejected(token(
                encoder, ISSUER,
                List.of(AUDIENCE + ".TenantSelection"), USER_ID.toString(),
                "tenant_selection", NOW.minusSeconds(600), NOW.minusSeconds(1)));
    }

    @Test
    void accessAndSelectionTokensCannotBeUsedInterchangeably() throws Exception {
        JwtTokenIssuer accessTokenIssuer = new JwtTokenIssuer(
                encoder,
                properties,
                Clock.fixed(NOW, ZoneOffset.UTC),
                () -> UUID.fromString("33333333-3333-3333-3333-333333333333"));
        String accessToken = accessTokenIssuer.issue(
                new JwtTokenIssuer.TokenRequest(USER_ID, null, List.of())).value();
        assertThat(service.validate(accessToken)).isEmpty();

        Instant currentTime = Instant.now();
        JwtTenantSelectionTokenService currentTimeService =
                new JwtTenantSelectionTokenService(
                        encoder,
                        rsaKey,
                        properties,
                        Clock.fixed(currentTime, ZoneOffset.UTC),
                        () -> TOKEN_ID);
        String selectionToken = currentTimeService.issue(USER_ID).value();
        NimbusJwtDecoder accessTokenDecoder = NimbusJwtDecoder
                .withPublicKey(rsaKey.toRSAPublicKey())
                .signatureAlgorithm(SignatureAlgorithm.RS256)
                .build();
        accessTokenDecoder.setJwtValidator(new JwtDecoderConfiguration().jwtValidator(
                new JwtSecurityProperties(true, "unused", ISSUER, AUDIENCE)));

        assertThatExceptionOfType(JwtException.class)
                .isThrownBy(() -> accessTokenDecoder.decode(selectionToken));
    }

    private void assertRejected(String token) {
        assertThat(service.validate(token)).isEmpty();
    }

    private static String token(
            JwtEncoder tokenEncoder,
            String issuer,
            List<String> audience,
            String subject,
            String purpose,
            Instant issuedAt,
            Instant expiresAt) {
        JwtClaimsSet.Builder claims = JwtClaimsSet.builder()
                .issuer(issuer)
                .audience(audience)
                .id(TOKEN_ID.toString())
                .issuedAt(issuedAt)
                .notBefore(issuedAt)
                .expiresAt(expiresAt)
                .claim("purpose", purpose);
        if (subject != null) {
            claims.subject(subject);
        }
        JwsHeader header = JwsHeader.with(SignatureAlgorithm.RS256)
                .keyId(KEY_ID)
                .build();
        return tokenEncoder.encode(JwtEncoderParameters.from(header, claims.build()))
                .getTokenValue();
    }

    private static RSAKey rsaKey(String keyId) throws Exception {
        KeyPairGenerator generator = KeyPairGenerator.getInstance("RSA");
        generator.initialize(2048);
        KeyPair keyPair = generator.generateKeyPair();
        return new RSAKey.Builder((RSAPublicKey) keyPair.getPublic())
                .privateKey((RSAPrivateKey) keyPair.getPrivate())
                .keyID(keyId)
                .build();
    }

    private static JwtEncoder encoder(RSAKey key) {
        return new NimbusJwtEncoder(new ImmutableJWKSet<>(new JWKSet(key)));
    }
}
