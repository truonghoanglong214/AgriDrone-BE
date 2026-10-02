package com.agridrone.be1.shared.auth;

import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.Objects;
import java.util.UUID;
import java.util.stream.Collectors;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.oauth2.jose.jws.SignatureAlgorithm;
import org.springframework.security.oauth2.jwt.JwtClaimsSet;
import org.springframework.security.oauth2.jwt.JwtEncoder;
import org.springframework.security.oauth2.jwt.JwtEncoderParameters;
import org.springframework.security.oauth2.jwt.JwsHeader;
import org.springframework.stereotype.Service;

@Service
@ConditionalOnProperty(
        prefix = "agridrone.security.jwt.issuer-service",
        name = "enabled",
        havingValue = "true")
public final class JwtTokenIssuer {
    private static final String SYSTEM_ROLE_CLAIM = "system_role";
    private static final String TENANT_ID_CLAIM = "tenant_id";
    private static final String TENANT_MEMBERSHIP_ID_CLAIM = "tenant_membership_id";
    private static final String TENANT_ROLE_CLAIM = "tenant_role";

    private final JwtEncoder encoder;
    private final JwtIssuerProperties properties;
    private final Clock clock;
    private final TokenIdGenerator tokenIds;

    @Autowired
    JwtTokenIssuer(JwtEncoder encoder, JwtIssuerProperties properties, Clock clock) {
        this(encoder, properties, clock, UUID::randomUUID);
    }

    public JwtTokenIssuer(
            JwtEncoder encoder,
            JwtIssuerProperties properties,
            Clock clock,
            TokenIdGenerator tokenIds) {
        this.encoder = encoder;
        this.properties = properties;
        this.clock = clock;
        this.tokenIds = tokenIds;
    }

    public IssuedJwt issue(TokenRequest request) {
        Instant issuedAt = clock.instant();
        Instant expiresAt = issuedAt.plus(properties.accessTokenTtl());
        JwtClaimsSet.Builder claims = JwtClaimsSet.builder()
                .issuer(properties.issuer())
                .audience(List.of(properties.audience()))
                .subject(request.subject().toString())
                .issuedAt(issuedAt)
                .notBefore(issuedAt)
                .expiresAt(expiresAt)
                .id(tokenIds.next().toString())
                .claim(SYSTEM_ROLE_CLAIM, request.systemRoles());
        if (request.tenantId() != null) {
            claims.claim(TENANT_ID_CLAIM, request.tenantId().toString());
        }
        if (request.tenantMembershipId() != null) {
            claims.claim(TENANT_MEMBERSHIP_ID_CLAIM, request.tenantMembershipId().toString());
        }
        if (request.tenantRole() != null && !request.tenantRole().isBlank()) {
            claims.claim(TENANT_ROLE_CLAIM, request.tenantRole());
        }

        JwsHeader header = JwsHeader.with(SignatureAlgorithm.RS256)
                .keyId(properties.keyId())
                .build();
        String token = encoder.encode(JwtEncoderParameters.from(header, claims.build())).getTokenValue();
        return new IssuedJwt(token, issuedAt, expiresAt);
    }

    public record TokenRequest(
            UUID subject,
            UUID tenantId,
            UUID tenantMembershipId,
            String tenantRole,
            List<String> systemRoles) {
        public TokenRequest(UUID subject, UUID tenantId, List<String> systemRoles) {
            this(subject, tenantId, null, null, systemRoles);
        }

        public TokenRequest {
            Objects.requireNonNull(subject, "subject");
            systemRoles = systemRoles == null ? List.of() : systemRoles.stream()
                    .filter(Objects::nonNull)
                    .map(String::trim)
                    .filter(role -> !role.isBlank())
                    .distinct()
                    .collect(Collectors.toUnmodifiableList());
        }
    }

    public record IssuedJwt(String value, Instant issuedAt, Instant expiresAt) {
    }
}
