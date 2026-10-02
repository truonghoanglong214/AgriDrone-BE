package com.agridrone.be1.shared.auth;

import java.time.Duration;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties("agridrone.security.jwt.issuer-service")
public record JwtIssuerProperties(
        boolean enabled,
        String issuer,
        String audience,
        String privateKeyLocation,
        String publicKeyLocation,
        String keyId,
        Duration accessTokenTtl,
        Duration tenantSelectionTokenTtl) {
}
