package com.agridrone.be1.shared.messaging;

import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.UUID;

public interface OutboxRepository {
    void enqueue(IntegrationEventEnvelope event, String routingKey, String partitionKey, byte[] body);
    List<OutboxMessage> claim(UUID workerId, int batchSize, Duration lease);
    void markPublished(UUID messageId, UUID workerId);
    void markFailed(OutboxMessage message, UUID workerId, Instant nextAttempt, int maxAttempts, String error);
    void redrive(UUID messageId);
}
