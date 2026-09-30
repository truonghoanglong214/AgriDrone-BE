package com.agridrone.be1.shared.messaging;

import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionSynchronizationManager;

@Service
@ConditionalOnProperty(name = "agridrone.runtime.enabled", havingValue = "true", matchIfMissing = true)
public class OutboxService {
    private final OutboxRepository repository;
    private final IntegrationEventSerializer serializer;

    public OutboxService(OutboxRepository repository, IntegrationEventSerializer serializer) {
        this.repository = repository;
        this.serializer = serializer;
    }

    public void enqueue(IntegrationEventEnvelope event, String routingKey, String partitionKey) {
        if (!TransactionSynchronizationManager.isActualTransactionActive()) {
            throw new IllegalStateException("Outbox messages must be enqueued inside the business transaction");
        }
        repository.enqueue(event, routingKey, partitionKey, serializer.serialize(event));
    }
}
