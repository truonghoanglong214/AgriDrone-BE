package com.agridrone.be1.shared.messaging;

import java.util.Optional;
import java.util.UUID;

public interface InboxRepository {
    Optional<String> findStatus(String consumerName, UUID messageId);
    void start(String consumerName, IntegrationEventEnvelope event);
    void complete(String consumerName, UUID messageId);
    void fail(String consumerName, UUID messageId, String errorCode, String errorMessage);
}
