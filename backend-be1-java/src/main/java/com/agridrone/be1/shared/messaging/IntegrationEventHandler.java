package com.agridrone.be1.shared.messaging;

public interface IntegrationEventHandler {
    String eventType();
    void handle(IntegrationEventEnvelope event);
}
