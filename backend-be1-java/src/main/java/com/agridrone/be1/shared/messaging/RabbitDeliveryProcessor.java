package com.agridrone.be1.shared.messaging;

import com.agridrone.be1.shared.execution.ActorType;
import com.agridrone.be1.shared.execution.ExecutionContext;
import com.agridrone.be1.shared.execution.ExecutionContextSnapshot;
import com.agridrone.be1.shared.execution.ExecutionSource;
import com.rabbitmq.client.Channel;
import java.io.IOException;
import java.util.List;
import java.util.Map;
import java.util.function.Function;
import java.util.stream.Collectors;
import org.springframework.stereotype.Component;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;

@Component
@ConditionalOnProperty(name = "agridrone.runtime.enabled", havingValue = "true", matchIfMissing = true)
public class RabbitDeliveryProcessor {
    public static final String RETRY_HEADER = "x-agridrone-retry-count";
    private final IntegrationEventSerializer serializer;
    private final InboxCoordinator inbox;
    private final ConfirmedEventPublisher publisher;
    private final MessagingProperties properties;
    private final Map<String, IntegrationEventHandler> handlers;

    public RabbitDeliveryProcessor(IntegrationEventSerializer serializer, InboxCoordinator inbox,
            ConfirmedEventPublisher publisher, MessagingProperties properties, List<IntegrationEventHandler> handlers) {
        this.serializer = serializer;
        this.inbox = inbox;
        this.publisher = publisher;
        this.properties = properties;
        this.handlers = handlers.stream().collect(Collectors.toUnmodifiableMap(
                IntegrationEventHandler::eventType, Function.identity()));
    }

    public void process(String consumerName, long deliveryTag, byte[] body, Map<String, Object> headers, Channel channel)
            throws IOException {
        MessagingProperties.Consumer consumer = properties.consumers().get(consumerName);
        if (consumer == null) throw new IllegalArgumentException("Unknown consumer: " + consumerName);
        try {
            IntegrationEventEnvelope event = serializer.deserialize(body);
            IntegrationEventHandler handler = handlers.get(event.eventType());
            if (handler == null) throw new PermanentMessageException("Messaging.HandlerMissing", "No handler for " + event.eventType());
            InboxCoordinator.Result result;
            try (ExecutionContext.Scope ignored = ExecutionContext.begin(new ExecutionContextSnapshot(
                    event.tenantId(), event.actorId(), event.actorId() == null ? ActorType.SYSTEM : ActorType.USER,
                    event.correlationId(), event.messageId(), ExecutionSource.RABBIT_MQ, java.util.Set.of()))) {
                result = inbox.process(consumerName, event, handler);
            }
            if (result == InboxCoordinator.Result.FAILED) {
                publisher.publish(properties.deadLetterExchange(), consumerName, body, headers);
            }
            channel.basicAck(deliveryTag, false);
        } catch (RuntimeException failure) {
            int attempt = retryCount(headers) + 1;
            try {
                if (attempt < consumer.maxAttempts()) {
                    publisher.publish(properties.retryExchange(), consumerName, body, Map.of(RETRY_HEADER, attempt));
                } else {
                    publisher.publish(properties.deadLetterExchange(), consumerName, body,
                            Map.of(RETRY_HEADER, attempt, "x-agridrone-error", truncate(failure.getMessage())));
                }
                channel.basicAck(deliveryTag, false);
            } catch (RuntimeException copyFailure) {
                channel.basicNack(deliveryTag, false, true);
            }
        }
    }

    private static int retryCount(Map<String, Object> headers) {
        Object value = headers == null ? null : headers.get(RETRY_HEADER);
        return value instanceof Number number ? number.intValue() : 0;
    }

    private static String truncate(String value) {
        if (value == null) return "Message processing failed";
        return value.length() <= 500 ? value : value.substring(0, 500);
    }
}
