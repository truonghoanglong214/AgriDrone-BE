package com.agridrone.be1.identity.infrastructure.config;

import com.agridrone.be1.identity.application.port.out.notification.PasswordResetPolicy;
import java.time.Duration;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties("agridrone.identity.password-reset")
public record PasswordResetProperties(
        String resetUrl,
        Duration expiration) implements PasswordResetPolicy {

    public PasswordResetProperties {
        if (resetUrl == null || resetUrl.isBlank()) {
            throw new IllegalArgumentException("Password reset URL must not be blank.");
        }
        if (expiration == null || expiration.isZero() || expiration.isNegative()) {
            throw new IllegalArgumentException("Password reset expiration must be positive.");
        }
    }
}
