package com.agridrone.be1.identity.infrastructure.config;

import com.agridrone.be1.identity.application.port.out.notification.TenantInvitationPolicy;
import java.time.Duration;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties("agridrone.identity.tenant-invitation")
public record TenantInvitationProperties(
        Duration expiration) implements TenantInvitationPolicy {

    public TenantInvitationProperties {
        if (expiration == null || expiration.isZero() || expiration.isNegative()
                || expiration.compareTo(Duration.ofDays(7)) > 0) {
            throw new IllegalArgumentException(
                    "Tenant invitation expiration must be between 1 millisecond and 7 days.");
        }
    }
}
