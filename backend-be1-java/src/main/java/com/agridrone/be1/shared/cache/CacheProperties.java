package com.agridrone.be1.shared.cache;

import java.time.Duration;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties("agridrone.cache")
public record CacheProperties(boolean enabled, String prefix, String version, Duration defaultTtl, Duration timeout) {
    public CacheProperties {
        if (prefix == null || prefix.isBlank() || version == null || version.isBlank()) {
            throw new IllegalArgumentException("Cache prefix and version are required");
        }
    }
}
