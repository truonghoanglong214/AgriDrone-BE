package com.agridrone.be1.shared.messaging;

import java.util.Map;

public interface ConfirmedEventPublisher {
    default void publish(String exchange, String routingKey, byte[] body) {
        publish(exchange, routingKey, body, Map.of());
    }

    void publish(String exchange, String routingKey, byte[] body, Map<String, Object> headers);
}
