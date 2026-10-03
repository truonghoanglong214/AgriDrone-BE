package com.agridrone.be1.identity.infrastructure.config;

import com.agridrone.be1.identity.application.port.out.config.SystemManagerInvitationPolicy;
import java.time.Duration;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties("agridrone.identity.system-manager-invitation")
public record SystemManagerInvitationProperties(
        String acceptUrl,
        Duration expiration) implements SystemManagerInvitationPolicy {

    public SystemManagerInvitationProperties {
        if (acceptUrl == null || acceptUrl.isBlank()) {
            throw new IllegalStateException(
                    "BE1_SYSTEM_MANAGER_INVITATION_URL must not be blank.");
        }
        if (expiration == null || expiration.isZero() || expiration.isNegative()) {
            throw new IllegalStateException(
                    "BE1_SYSTEM_MANAGER_INVITATION_EXPIRATION must be positive.");
        }
        if (expiration.compareTo(Duration.ofDays(7)) > 0) {
            throw new IllegalStateException(
                    "BE1_SYSTEM_MANAGER_INVITATION_EXPIRATION must not exceed 7 days.");
        }
    }
}
