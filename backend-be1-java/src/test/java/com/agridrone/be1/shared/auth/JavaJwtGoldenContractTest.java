package com.agridrone.be1.shared.auth;

import static org.assertj.core.api.Assertions.assertThat;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.nimbusds.jose.jwk.JWKSet;
import com.nimbusds.jose.jwk.RSAKey;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.core.io.DefaultResourceLoader;

class JavaJwtGoldenContractTest {
    private static final Instant ISSUED_AT = Instant.parse("2026-10-02T00:00:00Z");
    private static final UUID SUBJECT = UUID.fromString("11111111-1111-1111-1111-111111111111");
    private static final UUID TENANT = UUID.fromString("22222222-2222-2222-2222-222222222222");
    private static final UUID MEMBERSHIP = UUID.fromString("33333333-3333-3333-3333-333333333333");
    private static final UUID TOKEN_ID = UUID.fromString("44444444-4444-4444-4444-444444444444");

    private final ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();

    @Test
    void canonicalAccessTokenIsProducedByTheJavaIssuer() throws Exception {
        var properties = new JwtIssuerProperties(
                true,
                "https://auth.agridrone.test",
                "agridrone-api",
                "classpath:contracts/auth/java-rs256-contract-private.pem",
                "classpath:contracts/auth/java-rs256-contract-public.pem",
                "java-contract-2026-01",
                Duration.ofDays(36500),
                Duration.ofMinutes(5));
        var configuration = new JwtKeyConfiguration();
        RSAKey rsaKey = configuration.jwtRsaKey(properties, new DefaultResourceLoader());
        var issuer = new JwtTokenIssuer(
                configuration.jwtEncoder(rsaKey),
                properties,
                Clock.fixed(ISSUED_AT, ZoneOffset.UTC),
                () -> TOKEN_ID);

        var issued = issuer.issue(new JwtTokenIssuer.TokenRequest(
                SUBJECT,
                TENANT,
                MEMBERSHIP,
                "OWNER",
                List.of("SYSTEM_ADMIN")));
        JsonNode jwks = mapper.valueToTree(new JWKSet(rsaKey.toPublicJWK()).toJSONObject());

        Path fixturePath = Path.of(
                "..", "contracts", "examples", "auth", "java-issued-access-token.v1.json");
        assertThat(fixturePath).exists();
        JsonNode fixture = mapper.readTree(Files.readAllBytes(fixturePath));
        assertThat(fixture.path("token").asText()).isEqualTo(issued.value());
        assertThat(fixture.path("jwks")).isEqualTo(jwks);
        assertThat(fixture.path("issuer").asText()).isEqualTo(properties.issuer());
        assertThat(fixture.path("audience").asText()).isEqualTo(properties.audience());
        assertThat(fixture.path("expectedClaims").path("sub").asText())
                .isEqualTo(SUBJECT.toString());
        assertThat(fixture.path("expectedClaims").path("tenant_id").asText())
                .isEqualTo(TENANT.toString());
        assertThat(fixture.path("expectedClaims").path("tenant_membership_id").asText())
                .isEqualTo(MEMBERSHIP.toString());
        assertThat(fixture.path("expectedClaims").path("tenant_role").asText())
                .isEqualTo("OWNER");
        assertThat(fixture.path("expectedClaims").path("system_role").get(0).asText())
                .isEqualTo("SYSTEM_ADMIN");
    }
}
