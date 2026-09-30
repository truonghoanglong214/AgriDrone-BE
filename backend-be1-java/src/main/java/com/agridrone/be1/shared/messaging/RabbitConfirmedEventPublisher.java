package com.agridrone.be1.shared.messaging;

import java.util.UUID;
import java.util.Map;
import java.util.concurrent.TimeUnit;
import org.springframework.amqp.core.Message;
import org.springframework.amqp.core.MessageBuilder;
import org.springframework.amqp.core.MessageDeliveryMode;
import org.springframework.amqp.rabbit.connection.CorrelationData;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Component;

@Component
@ConditionalOnProperty(name = "agridrone.runtime.enabled", havingValue = "true", matchIfMissing = true)
public class RabbitConfirmedEventPublisher implements ConfirmedEventPublisher {
    private final RabbitTemplate rabbitTemplate;
    private final MessagingProperties properties;

    public RabbitConfirmedEventPublisher(RabbitTemplate rabbitTemplate, MessagingProperties properties) {
        this.rabbitTemplate = rabbitTemplate;
        this.properties = properties;
        this.rabbitTemplate.setMandatory(true);
    }

    @Override
    public void publish(String exchange, String routingKey, byte[] body, Map<String, Object> headers) {
        CorrelationData correlation = new CorrelationData(UUID.randomUUID().toString());
        Message message = MessageBuilder.withBody(body)
                .setContentType("application/json")
                .setDeliveryMode(MessageDeliveryMode.PERSISTENT)
                .copyHeaders(headers)
                .build();
        rabbitTemplate.send(exchange, routingKey, message, correlation);
        try {
            CorrelationData.Confirm confirm = correlation.getFuture().get(
                    properties.outbox().confirmTimeout().toMillis(), TimeUnit.MILLISECONDS);
            if (!confirm.isAck()) {
                throw new IllegalStateException("RabbitMQ rejected publish: " + confirm.getReason());
            }
            if (correlation.getReturned() != null) {
                throw new IllegalStateException("RabbitMQ returned unroutable message: "
                        + correlation.getReturned().getReplyText());
            }
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException("Interrupted while awaiting RabbitMQ confirm", exception);
        } catch (Exception exception) {
            if (exception instanceof IllegalStateException state) {
                throw state;
            }
            throw new IllegalStateException("RabbitMQ publish was not confirmed", exception);
        }
    }
}
