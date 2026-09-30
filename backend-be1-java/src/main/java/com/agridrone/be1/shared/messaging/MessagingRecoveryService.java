package com.agridrone.be1.shared.messaging;

import com.agridrone.be1.shared.audit.AuditEntry;
import com.agridrone.be1.shared.audit.AuditWriter;
import java.util.UUID;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@ConditionalOnProperty(name = "agridrone.messaging.enabled", havingValue = "true")
public class MessagingRecoveryService {
    private final OutboxRepository outbox;
    private final AuditWriter audit;
    private final RabbitTemplate rabbit;
    private final ConfirmedEventPublisher publisher;
    private final MessagingProperties properties;

    public MessagingRecoveryService(OutboxRepository outbox, AuditWriter audit, RabbitTemplate rabbit,
            ConfirmedEventPublisher publisher, MessagingProperties properties) {
        this.outbox = outbox;
        this.audit = audit;
        this.rabbit = rabbit;
        this.publisher = publisher;
        this.properties = properties;
    }

    @Transactional
    public void redriveOutbox(UUID messageId, String reason) {
        outbox.redrive(messageId);
        audit.append(new AuditEntry(null, "OutboxMessage", messageId, "REDRIVE", null, null, reason));
    }

    public boolean redriveDeadLetter(String consumerName) {
        MessagingProperties.Consumer consumer = properties.consumers().get(consumerName);
        if (consumer == null) throw new IllegalArgumentException("Unknown consumer: " + consumerName);
        return Boolean.TRUE.equals(rabbit.execute(channel -> {
            var response = channel.basicGet(consumer.queue() + ".dead", false);
            if (response == null) return false;
            try {
                publisher.publish(properties.exchange(), consumer.routingKey(), response.getBody(),
                        response.getProps().getHeaders());
                channel.basicAck(response.getEnvelope().getDeliveryTag(), false);
                return true;
            } catch (RuntimeException failure) {
                channel.basicNack(response.getEnvelope().getDeliveryTag(), false, true);
                throw failure;
            }
        }));
    }
}
