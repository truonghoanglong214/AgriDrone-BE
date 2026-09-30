package com.agridrone.be1.shared.messaging;

import java.util.ArrayList;
import java.util.List;
import org.springframework.amqp.core.AcknowledgeMode;
import org.springframework.amqp.rabbit.connection.ConnectionFactory;
import org.springframework.amqp.rabbit.listener.SimpleMessageListenerContainer;
import org.springframework.amqp.rabbit.listener.api.ChannelAwareMessageListener;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.SmartLifecycle;
import org.springframework.stereotype.Component;

@Component
@ConditionalOnProperty(name = "agridrone.messaging.enabled", havingValue = "true")
public class RabbitConsumerContainers implements SmartLifecycle {
    private final List<SimpleMessageListenerContainer> containers = new ArrayList<>();
    private volatile boolean running;

    public RabbitConsumerContainers(ConnectionFactory connectionFactory, MessagingProperties properties,
            RabbitDeliveryProcessor processor) {
        properties.consumers().forEach((name, settings) -> {
            SimpleMessageListenerContainer container = new SimpleMessageListenerContainer(connectionFactory);
            container.setQueueNames(settings.queue());
            container.setAcknowledgeMode(AcknowledgeMode.MANUAL);
            container.setDefaultRequeueRejected(false);
            container.setMessageListener((ChannelAwareMessageListener) (message, channel) -> processor.process(
                    name,
                    message.getMessageProperties().getDeliveryTag(),
                    message.getBody(),
                    message.getMessageProperties().getHeaders(),
                    channel));
            containers.add(container);
        });
    }

    @Override
    public void start() {
        containers.forEach(SimpleMessageListenerContainer::start);
        running = true;
    }

    @Override
    public void stop() {
        containers.forEach(SimpleMessageListenerContainer::stop);
        running = false;
    }

    @Override
    public boolean isRunning() {
        return running;
    }

    @Override
    public boolean isAutoStartup() {
        return true;
    }

    @Override
    public int getPhase() {
        return Integer.MAX_VALUE - 100;
    }
}
