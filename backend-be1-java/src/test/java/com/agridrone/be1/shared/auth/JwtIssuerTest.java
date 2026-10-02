package com.agridrone.be1.shared.auth;

import static org.assertj.core.api.Assertions.assertThat;

import com.agridrone.be1.shared.api.JwksController;
import com.nimbusds.jose.jwk.JWKSet;
import com.nimbusds.jose.jwk.RSAKey;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.security.KeyPair;
import java.security.KeyPairGenerator;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import org.springframework.core.io.DefaultResourceLoader;
import org.springframework.security.oauth2.core.OAuth2TokenValidatorResult;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.NimbusJwtDecoder;

class JwtIssuerTest {

    @TempDir
    Path temporaryDirectory;

    @Test
    void signsRs256TokenWithConfiguredClaimsAndExposesOnlyPublicJwk() throws Exception {
        KeyPairGenerator generator = KeyPairGenerator.getInstance("RSA");
        generator.initialize(2048);
        KeyPair keyPair = generator.generateKeyPair();
        Path privateKey = writePem("PRIVATE KEY", keyPair.getPrivate().getEncoded());
        Path publicKey = writePem("PUBLIC KEY", keyPair.getPublic().getEncoded());
        JwtIssuerProperties properties = new JwtIssuerProperties(
                true,
                "http://issuer.test",
                "agridrone-api",
                privateKey.toUri().toString(),
                publicKey.toUri().toString(),
                "test-key",
                Duration.ofMinutes(15),
                Duration.ofMinutes(5));

        JwtKeyConfiguration configuration = new JwtKeyConfiguration();
        RSAKey rsaKey = configuration.jwtRsaKey(properties, new DefaultResourceLoader());
        JwtTokenIssuer issuer = new JwtTokenIssuer(
                configuration.jwtEncoder(rsaKey),
                properties,
                Clock.systemUTC());
        UUID subject = UUID.randomUUID();
        UUID tenant = UUID.randomUUID();

        JwtTokenIssuer.IssuedJwt issued = issuer.issue(
                new JwtTokenIssuer.TokenRequest(subject, tenant, List.of("SYSTEM_ADMIN")));

        NimbusJwtDecoder decoder = NimbusJwtDecoder.withPublicKey(
                        (java.security.interfaces.RSAPublicKey) keyPair.getPublic())
                .build();
        decoder.setJwtValidator(jwt -> OAuth2TokenValidatorResult.success());
        Jwt decoded = decoder.decode(issued.value());

        assertThat(decoded.getHeaders()).containsEntry("alg", "RS256");
        assertThat(decoded.getHeaders()).containsEntry("kid", "test-key");
        assertThat(decoded.getIssuer()).hasToString("http://issuer.test");
        assertThat(decoded.getAudience()).containsExactly("agridrone-api");
        assertThat(decoded.getSubject()).isEqualTo(subject.toString());
        assertThat(decoded.getClaimAsString("tenant_id")).isEqualTo(tenant.toString());
        assertThat(decoded.getClaimAsStringList("system_role")).containsExactly("SYSTEM_ADMIN");

        JWKSet publicKeySet = new JWKSet(rsaKey.toPublicJWK());
        assertThat(publicKeySet.getKeys()).hasSize(1);
        assertThat(publicKeySet.getKeys().getFirst().isPrivate()).isFalse();
        assertThat(publicKeySet.getKeys().getFirst().getKeyID()).isEqualTo("test-key");

        var jwksResponse = new JwksController(rsaKey).jwks();
        assertThat(jwksResponse.getStatusCode().is2xxSuccessful()).isTrue();
        assertThat(jwksResponse.getBody()).containsKey("keys");
        assertThat(jwksResponse.getBody().get("keys").toString()).contains("test-key");
    }

    private Path writePem(String type, byte[] encoded) throws Exception {
        String body = java.util.Base64.getMimeEncoder(64, "\n".getBytes(StandardCharsets.US_ASCII))
                .encodeToString(encoded);
        Path path = temporaryDirectory.resolve(type.toLowerCase().replace(' ', '-') + ".pem");
        Files.writeString(path, "-----BEGIN " + type + "-----\n"
                + body + "\n-----END " + type + "-----\n", StandardCharsets.US_ASCII);
        return path;
    }
}
