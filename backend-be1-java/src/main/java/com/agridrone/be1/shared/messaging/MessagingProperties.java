package com.agridrone.be1.shared.messaging;

import java.time.Duration;
import java.util.Map;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties("agridrone.messaging")
public record MessagingProperties(
        boolean enabled,
        String exchange,
        String retryExchange,
        String deadLetterExchange,
        Outbox outbox,
        Map<String, Consumer> consumers) {

    public MessagingProperties {
        consumers = consumers == null ? Map.of() : Map.copyOf(consumers);
    }

    public record Outbox(int batchSize, Duration lease, Duration pollDelay, Duration confirmTimeout,
                         int maxAttempts, Duration retryBase, Duration retryMax) {
    }

    public record Consumer(String queue, String routingKey, int maxAttempts, Duration retryDelay) {
    }
}
