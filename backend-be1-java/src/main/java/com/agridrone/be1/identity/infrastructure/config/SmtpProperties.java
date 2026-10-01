package com.agridrone.be1.identity.infrastructure.config;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties("agridrone.notification.smtp")
public record SmtpProperties(
        boolean enabled,
        String host,
        int port,
        String securityMode,
        String username,
        String password,
        String fromAddress,
        String fromName,
        int timeoutSeconds) {
}
