package com.agridrone.be1.identity.infrastructure.security.jwt;

import com.agridrone.be1.identity.application.port.out.security.AccessTokenIssuer;
import com.agridrone.be1.identity.application.security.IssuedAccessToken;
import com.agridrone.be1.shared.auth.JwtTokenIssuer;
import java.util.List;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Component;

@Component
@ConditionalOnProperty(
        prefix = "agridrone.security.jwt.issuer-service",
        name = "enabled",
        havingValue = "true")
public class JwtAccessTokenIssuerAdapter implements AccessTokenIssuer {
    private final JwtTokenIssuer issuer;

    public JwtAccessTokenIssuerAdapter(JwtTokenIssuer issuer) {
        this.issuer = issuer;
    }

    @Override
    public IssuedAccessToken issue(UUID subject, UUID tenantId, UUID tenantMembershipId,
            String tenantRole, List<String> systemRoles) {
        JwtTokenIssuer.IssuedJwt token = issuer.issue(new JwtTokenIssuer.TokenRequest(
                subject, tenantId, tenantMembershipId, tenantRole, systemRoles));
        return new IssuedAccessToken(token.value(), token.issuedAt(), token.expiresAt());
    }
}
