package com.agridrone.be1;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.agridrone.be1.shared.messaging.MessagingProperties;
import com.agridrone.be1.shared.messaging.RabbitConfirmedEventPublisher;
import java.time.Duration;
import java.util.Map;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.amqp.core.BindingBuilder;
import org.springframework.amqp.core.Queue;
import org.springframework.amqp.core.TopicExchange;
import org.springframework.amqp.rabbit.connection.CachingConnectionFactory;
import org.springframework.amqp.rabbit.core.RabbitAdmin;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.testcontainers.containers.RabbitMQContainer;
import org.testcontainers.junit.jupiter.Container;
import org.testcontainers.junit.jupiter.Testcontainers;

@Testcontainers(disabledWithoutDocker = true)
class RabbitPublisherConfirmIT {
    @Container
    static final RabbitMQContainer RABBIT = new RabbitMQContainer("rabbitmq:4.2.7-management-alpine");

    @Test
    void routedPublishIsConfirmedAndMandatoryUnroutablePublishFails() {
        CachingConnectionFactory connection = new CachingConnectionFactory(RABBIT.getHost(), RABBIT.getAmqpPort());
        connection.setUsername(RABBIT.getAdminUsername());
        connection.setPassword(RABBIT.getAdminPassword());
        connection.setPublisherConfirmType(CachingConnectionFactory.ConfirmType.CORRELATED);
        connection.setPublisherReturns(true);
        try {
            String exchangeName = "phase3." + UUID.randomUUID();
            String queueName = "phase3." + UUID.randomUUID();
            TopicExchange exchange = new TopicExchange(exchangeName, false, true);
            Queue queue = new Queue(queueName, false, false, true);
            RabbitAdmin admin = new RabbitAdmin(connection);
            admin.declareExchange(exchange);
            admin.declareQueue(queue);
            admin.declareBinding(BindingBuilder.bind(queue).to(exchange).with("contract.test"));
            RabbitTemplate template = new RabbitTemplate(connection);
            var publisher = new RabbitConfirmedEventPublisher(template, properties(exchangeName));

            publisher.publish(exchangeName, "contract.test", "{}".getBytes());
            assertThat(template.receive(queueName, 5_000)).isNotNull();
            assertThatThrownBy(() -> publisher.publish(exchangeName, "unroutable", "{}".getBytes()))
                    .isInstanceOf(IllegalStateException.class)
                    .hasMessageContaining("unroutable");
        } finally {
            connection.destroy();
        }
    }

    private static MessagingProperties properties(String exchange) {
        return new MessagingProperties(true, exchange, "retry", "dead",
                new MessagingProperties.Outbox(10, Duration.ofSeconds(10), Duration.ofSeconds(1),
                        Duration.ofSeconds(5), 3, Duration.ofSeconds(1), Duration.ofSeconds(10)), Map.of());
    }
}
