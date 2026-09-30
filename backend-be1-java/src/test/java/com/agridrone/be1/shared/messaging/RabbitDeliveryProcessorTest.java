package com.agridrone.be1.shared.messaging;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.inOrder;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.rabbitmq.client.Channel;
import java.time.Duration;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.mockito.InOrder;

class RabbitDeliveryProcessorTest {
    @Test
    void acknowledgesOnlyAfterInboxTransactionReturnsCommitted() throws Exception {
        ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
        IntegrationEventSerializer serializer = new IntegrationEventSerializer(mapper);
        IntegrationEventEnvelope event = new IntegrationEventEnvelope(
                UUID.randomUUID(), UUID.randomUUID(), UUID.randomUUID(), null,
                OffsetDateTime.now(), 1, "notification.email-requested.v1", mapper.createObjectNode());
        IntegrationEventHandler handler = new IntegrationEventHandler() {
            public String eventType() { return event.eventType(); }
            public void handle(IntegrationEventEnvelope ignored) { }
        };
        InboxCoordinator inbox = mock(InboxCoordinator.class);
        ConfirmedEventPublisher publisher = mock(ConfirmedEventPublisher.class);
        Channel channel = mock(Channel.class);
        when(inbox.process(eq("notifications"), any(), eq(handler)))
                .thenReturn(InboxCoordinator.Result.COMPLETED);
        RabbitDeliveryProcessor processor = new RabbitDeliveryProcessor(
                serializer, inbox, publisher, properties(), List.of(handler));

        processor.process("notifications", 7L, serializer.serialize(event), Map.of(), channel);

        InOrder order = inOrder(inbox, channel);
        order.verify(inbox).process(eq("notifications"), any(), eq(handler));
        order.verify(channel).basicAck(7L, false);
    }

    private static MessagingProperties properties() {
        return new MessagingProperties(true, "events", "retry", "dead",
                new MessagingProperties.Outbox(10, Duration.ofSeconds(10), Duration.ofSeconds(1),
                        Duration.ofSeconds(5), 3, Duration.ofSeconds(1), Duration.ofSeconds(10)),
                Map.of("notifications", new MessagingProperties.Consumer(
                        "be1.notifications", "notification.#", 3, Duration.ofSeconds(5))));
    }
}
