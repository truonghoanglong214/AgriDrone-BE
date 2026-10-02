package com.agridrone.be1.identity.infrastructure.security.jwt;

import com.agridrone.be1.identity.application.port.out.security.TenantSelectionTokenService;
import com.agridrone.be1.identity.application.security.IssuedTenantSelectionToken;
import com.agridrone.be1.shared.auth.JwtIssuerProperties;
import com.agridrone.be1.shared.auth.TokenIdGenerator;
import com.nimbusds.jose.JOSEException;
import com.nimbusds.jose.jwk.RSAKey;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.security.oauth2.core.DelegatingOAuth2TokenValidator;
import org.springframework.security.oauth2.core.OAuth2Error;
import org.springframework.security.oauth2.core.OAuth2TokenValidatorResult;
import org.springframework.security.oauth2.jose.jws.SignatureAlgorithm;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.jwt.JwtClaimValidator;
import org.springframework.security.oauth2.jwt.JwtDecoder;
import org.springframework.security.oauth2.jwt.JwtEncoder;
import org.springframework.security.oauth2.jwt.JwtEncoderParameters;
import org.springframework.security.oauth2.jwt.JwtException;
import org.springframework.security.oauth2.jwt.JwtIssuerValidator;
import org.springframework.security.oauth2.jwt.JwtTimestampValidator;
import org.springframework.security.oauth2.jwt.JwsHeader;
import org.springframework.security.oauth2.jwt.NimbusJwtDecoder;
import org.springframework.security.oauth2.jwt.JwtClaimsSet;
import org.springframework.stereotype.Component;
import org.springframework.util.StringUtils;

@Component
@ConditionalOnProperty(
        prefix = "agridrone.security.jwt.issuer-service",
        name = "enabled",
        havingValue = "true")
public final class JwtTenantSelectionTokenService implements TenantSelectionTokenService {
    public static final String PURPOSE = "tenant_selection";
    public static final String PURPOSE_CLAIM = "purpose";
    public static final String AUDIENCE_SUFFIX = ".TenantSelection";

    private final JwtEncoder encoder;
    private final JwtDecoder decoder;
    private final JwtIssuerProperties properties;
    private final Clock clock;
    private final TokenIdGenerator tokenIds;

    public JwtTenantSelectionTokenService(
            JwtEncoder encoder,
            RSAKey rsaKey,
            JwtIssuerProperties properties,
            Clock clock,
            TokenIdGenerator tokenIds) {
        this.encoder = encoder;
        this.properties = properties;
        this.clock = clock;
        this.tokenIds = tokenIds;
        this.decoder = createDecoder(rsaKey);
    }

    @Override
    public IssuedTenantSelectionToken issue(UUID userId) {
        Instant issuedAt = clock.instant();
        Instant expiresAt = issuedAt.plus(properties.tenantSelectionTokenTtl());
        JwtClaimsSet claims = JwtClaimsSet.builder()
                .issuer(properties.issuer())
                .audience(List.of(selectionAudience()))
                .subject(userId.toString())
                .id(tokenIds.next().toString())
                .issuedAt(issuedAt)
                .notBefore(issuedAt)
                .expiresAt(expiresAt)
                .claim(PURPOSE_CLAIM, PURPOSE)
                .build();
        JwsHeader header = JwsHeader.with(SignatureAlgorithm.RS256)
                .keyId(properties.keyId())
                .build();
        String value = encoder.encode(JwtEncoderParameters.from(header, claims)).getTokenValue();
        return new IssuedTenantSelectionToken(value, issuedAt, expiresAt);
    }

    @Override
    public Optional<UUID> validate(String token) {
        if (!StringUtils.hasText(token)) {
            return Optional.empty();
        }
        try {
            return Optional.of(UUID.fromString(decoder.decode(token).getSubject()));
        } catch (JwtException | IllegalArgumentException exception) {
            return Optional.empty();
        }
    }

    private JwtDecoder createDecoder(RSAKey rsaKey) {
        try {
            NimbusJwtDecoder jwtDecoder = NimbusJwtDecoder
                    .withPublicKey(rsaKey.toRSAPublicKey())
                    .signatureAlgorithm(SignatureAlgorithm.RS256)
                    .build();
            JwtTimestampValidator timestampValidator = new JwtTimestampValidator(Duration.ZERO);
            timestampValidator.setClock(clock);
            jwtDecoder.setJwtValidator(new DelegatingOAuth2TokenValidator<>(
                    timestampValidator,
                    new JwtIssuerValidator(properties.issuer()),
                    new JwtClaimValidator<List<String>>("aud",
                            audience -> audience != null && audience.contains(selectionAudience())),
                    new JwtClaimValidator<String>(PURPOSE_CLAIM, PURPOSE::equals),
                    new JwtClaimValidator<String>("sub", JwtTenantSelectionTokenService::isUuid),
                    new JwtClaimValidator<String>("jti", StringUtils::hasText),
                    new JwtClaimValidator<Instant>("iat", value -> value != null),
                    new JwtClaimValidator<Instant>("nbf", value -> value != null),
                    new JwtClaimValidator<Instant>("exp", value -> value != null),
                    this::validateHeader));
            return jwtDecoder;
        } catch (JOSEException exception) {
            throw new IllegalStateException("Unable to create the tenant-selection JWT decoder.", exception);
        }
    }

    private OAuth2TokenValidatorResult validateHeader(Jwt jwt) {
        Object algorithm = jwt.getHeaders().get("alg");
        Object keyId = jwt.getHeaders().get("kid");
        if (!SignatureAlgorithm.RS256.getName().equals(algorithm)
                || !properties.keyId().equals(keyId)) {
            return OAuth2TokenValidatorResult.failure(new OAuth2Error(
                    "invalid_token",
                    "Tenant-selection JWT must use the configured RS256 key.",
                    null));
        }
        return OAuth2TokenValidatorResult.success();
    }

    private String selectionAudience() {
        return properties.audience() + AUDIENCE_SUFFIX;
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
