package com.agridrone.be1.shared.messaging;

import java.util.UUID;

public record OutboxMessage(
        UUID messageId,
        String routingKey,
        byte[] body,
        int attemptCount) {
}
