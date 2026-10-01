package com.agridrone.be1.shared.auth;

import com.nimbusds.jose.JWSAlgorithm;
import com.nimbusds.jose.jwk.JWKSet;
import com.nimbusds.jose.jwk.KeyUse;
import com.nimbusds.jose.jwk.RSAKey;
import com.nimbusds.jose.jwk.source.ImmutableJWKSet;
import com.nimbusds.jose.jwk.source.JWKSource;
import com.nimbusds.jose.proc.SecurityContext;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.security.KeyFactory;
import java.security.NoSuchAlgorithmException;
import java.security.GeneralSecurityException;
import java.security.interfaces.RSAPrivateKey;
import java.security.interfaces.RSAPublicKey;
import java.security.spec.PKCS8EncodedKeySpec;
import java.security.spec.X509EncodedKeySpec;
import java.util.Base64;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.core.io.Resource;
import org.springframework.core.io.ResourceLoader;
import org.springframework.security.oauth2.jwt.JwtEncoder;
import org.springframework.security.oauth2.jwt.NimbusJwtEncoder;
import org.springframework.util.StringUtils;

@Configuration(proxyBeanMethods = false)
@ConditionalOnProperty(
        prefix = "agridrone.security.jwt.issuer-service",
        name = "enabled",
        havingValue = "true")
public class JwtKeyConfiguration {

    @Bean
    RSAKey jwtRsaKey(JwtIssuerProperties properties, ResourceLoader resourceLoader) {
        validate(properties);
        try {
            RSAPrivateKey privateKey = readPrivateKey(
                    resourceLoader.getResource(properties.privateKeyLocation()));
            RSAPublicKey publicKey = readPublicKey(
                    resourceLoader.getResource(properties.publicKeyLocation()));
            if (!publicKey.getModulus().equals(privateKey.getModulus())) {
                throw new IllegalStateException("Configured RSA public and private keys do not match.");
            }
            return new RSAKey.Builder(publicKey)
                    .privateKey(privateKey)
                    .keyID(properties.keyId())
                    .algorithm(JWSAlgorithm.RS256)
                    .keyUse(KeyUse.SIGNATURE)
                    .build();
        } catch (IOException | GeneralSecurityException exception) {
            throw new IllegalStateException("Unable to load the configured RSA JWT keys.", exception);
        }
    }

    @Bean
    JwtEncoder jwtEncoder(RSAKey rsaKey) {
        JWKSource<SecurityContext> source = new ImmutableJWKSet<>(new JWKSet(rsaKey));
        return new NimbusJwtEncoder(source);
    }

    private static void validate(JwtIssuerProperties properties) {
        require(properties.issuer(), "BE1_JWT_ISSUER");
        require(properties.audience(), "BE1_JWT_AUDIENCE");
        require(properties.privateKeyLocation(), "BE1_JWT_PRIVATE_KEY_LOCATION");
        require(properties.publicKeyLocation(), "BE1_JWT_PUBLIC_KEY_LOCATION");
        require(properties.keyId(), "BE1_JWT_KEY_ID");
        if (properties.accessTokenTtl() == null || properties.accessTokenTtl().isZero()
                || properties.accessTokenTtl().isNegative()) {
            throw new IllegalStateException("BE1_JWT_ACCESS_TOKEN_TTL must be positive.");
        }
    }

    private static void require(String value, String environmentVariable) {
        if (!StringUtils.hasText(value)) {
            throw new IllegalStateException(environmentVariable + " is required when JWT issuer is enabled.");
        }
    }

    private static RSAPrivateKey readPrivateKey(Resource resource)
            throws IOException, GeneralSecurityException {
        byte[] encoded = decodePem(resource, "PRIVATE KEY");
        return (RSAPrivateKey) keyFactory().generatePrivate(new PKCS8EncodedKeySpec(encoded));
    }

    private static RSAPublicKey readPublicKey(Resource resource)
            throws IOException, GeneralSecurityException {
        byte[] encoded = decodePem(resource, "PUBLIC KEY");
        return (RSAPublicKey) keyFactory().generatePublic(new X509EncodedKeySpec(encoded));
    }

    private static byte[] decodePem(Resource resource, String type) throws IOException {
        if (!resource.exists()) {
            throw new IOException("Key resource does not exist: " + resource.getDescription());
        }
        String pem;
        try (var input = resource.getInputStream()) {
            pem = new String(input.readAllBytes(), StandardCharsets.US_ASCII);
        }
        String begin = "-----BEGIN " + type + "-----";
        String end = "-----END " + type + "-----";
        if (!pem.contains(begin) || !pem.contains(end)) {
            throw new IOException("Expected PEM " + type + " in " + resource.getDescription());
        }
        String content = pem.replace(begin, "").replace(end, "").replaceAll("\\s", "");
        try {
            return Base64.getDecoder().decode(content);
        } catch (IllegalArgumentException exception) {
            throw new IOException("Invalid PEM " + type + " in " + resource.getDescription(), exception);
        }
    }

    private static KeyFactory keyFactory() throws NoSuchAlgorithmException {
        return KeyFactory.getInstance("RSA");
    }

}
