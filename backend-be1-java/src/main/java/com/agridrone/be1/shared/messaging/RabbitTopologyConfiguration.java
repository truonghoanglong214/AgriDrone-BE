package com.agridrone.be1.shared.messaging;

import java.util.ArrayList;
import java.util.List;
import org.springframework.amqp.core.BindingBuilder;
import org.springframework.amqp.core.Declarable;
import org.springframework.amqp.core.Declarables;
import org.springframework.amqp.core.DirectExchange;
import org.springframework.amqp.core.Queue;
import org.springframework.amqp.core.QueueBuilder;
import org.springframework.amqp.core.TopicExchange;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
@ConditionalOnProperty(name = "agridrone.messaging.enabled", havingValue = "true")
public class RabbitTopologyConfiguration {
    @Bean
    Declarables messagingTopology(MessagingProperties properties) {
        TopicExchange events = new TopicExchange(properties.exchange(), true, false);
        DirectExchange retry = new DirectExchange(properties.retryExchange(), true, false);
        DirectExchange dead = new DirectExchange(properties.deadLetterExchange(), true, false);
        List<Declarable> declarations = new ArrayList<>(List.of(events, retry, dead));
        properties.consumers().forEach((name, config) -> {
            Queue queue = QueueBuilder.durable(config.queue()).build();
            Queue retryQueue = QueueBuilder.durable(config.queue() + ".retry")
                    .ttl((int) config.retryDelay().toMillis())
                    .deadLetterExchange(properties.exchange())
                    .deadLetterRoutingKey(config.routingKey())
                    .build();
            Queue deadQueue = QueueBuilder.durable(config.queue() + ".dead").build();
            declarations.addAll(List.of(queue, retryQueue, deadQueue,
                    BindingBuilder.bind(queue).to(events).with(config.routingKey()),
                    BindingBuilder.bind(retryQueue).to(retry).with(name),
                    BindingBuilder.bind(deadQueue).to(dead).with(name)));
        });
        return new Declarables(declarations);
    }
}
