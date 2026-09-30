package com.agridrone.be1.shared.messaging;

import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Component;
import org.springframework.transaction.support.TransactionTemplate;

@Component
@ConditionalOnProperty(name = "agridrone.runtime.enabled", havingValue = "true", matchIfMissing = true)
public class InboxCoordinator {
    private final InboxRepository repository;
    private final TransactionTemplate transactions;

    public InboxCoordinator(InboxRepository repository, TransactionTemplate transactions) {
        this.repository = repository;
        this.transactions = transactions;
    }

    public Result process(String consumerName, IntegrationEventEnvelope event, IntegrationEventHandler handler) {
        return transactions.execute(status -> {
            if (repository.findStatus(consumerName, event.messageId()).isPresent()) {
                return Result.DUPLICATE;
            }
            repository.start(consumerName, event);
            try {
                handler.handle(event);
                repository.complete(consumerName, event.messageId());
                return Result.COMPLETED;
            } catch (PermanentMessageException failure) {
                repository.fail(consumerName, event.messageId(), failure.code(), failure.getMessage());
                return Result.FAILED;
            }
        });
    }

    public enum Result { COMPLETED, DUPLICATE, FAILED }
}
