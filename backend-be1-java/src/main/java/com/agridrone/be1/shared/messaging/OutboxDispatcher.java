package com.agridrone.be1.shared.messaging;

import io.micrometer.core.instrument.MeterRegistry;
import java.time.Duration;
import java.time.Instant;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

@Component
@ConditionalOnProperty(name = "agridrone.messaging.enabled", havingValue = "true")
public class OutboxDispatcher {
    private final UUID workerId = UUID.randomUUID();
    private final OutboxRepository repository;
    private final ConfirmedEventPublisher publisher;
    private final MessagingProperties properties;
    private final MeterRegistry meters;

    public OutboxDispatcher(OutboxRepository repository, ConfirmedEventPublisher publisher,
                            MessagingProperties properties, MeterRegistry meters) {
        this.repository = repository;
        this.publisher = publisher;
        this.properties = properties;
        this.meters = meters;
    }

    @Scheduled(fixedDelayString = "${agridrone.messaging.outbox.poll-delay:1s}")
    public void dispatch() {
        MessagingProperties.Outbox settings = properties.outbox();
        for (OutboxMessage message : repository.claim(workerId, settings.batchSize(), settings.lease())) {
            try {
                publisher.publish(properties.exchange(), message.routingKey(), message.body());
                repository.markPublished(message.messageId(), workerId);
                meters.counter("agridrone.outbox.published").increment();
            } catch (RuntimeException failure) {
                Duration delay = retryDelay(settings, message.attemptCount());
                repository.markFailed(message, workerId, Instant.now().plus(delay), settings.maxAttempts(), failure.getMessage());
                meters.counter("agridrone.outbox.failed").increment();
            }
        }
    }

    static Duration retryDelay(MessagingProperties.Outbox settings, int attempt) {
        long multiplier = 1L << Math.min(Math.max(attempt - 1, 0), 20);
        long millis = Math.min(settings.retryMax().toMillis(), settings.retryBase().toMillis() * multiplier);
        return Duration.ofMillis(millis);
    }
}
