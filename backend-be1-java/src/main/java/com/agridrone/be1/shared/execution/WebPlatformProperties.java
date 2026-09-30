package com.agridrone.be1.shared.execution;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties("agridrone.web")
public record WebPlatformProperties(String correlationHeader) {

    public WebPlatformProperties {
        if (correlationHeader == null || correlationHeader.isBlank()) {
            correlationHeader = "X-Correlation-ID";
        }
    }
}
