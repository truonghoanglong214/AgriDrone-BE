package com.agridrone.be1.shared.auth;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties("agridrone.security.jwt")
public record JwtSecurityProperties(boolean enabled, String jwkSetUri, String issuer, String audience) {
}
