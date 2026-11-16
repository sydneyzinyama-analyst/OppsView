package com.oppsview.notifier.events;

import com.oppsview.notifier.config.MessagingConfig;
import org.springframework.amqp.AmqpRejectAndDontRequeueException;
import org.springframework.amqp.core.Message;
import org.springframework.amqp.rabbit.annotation.RabbitListener;
import org.springframework.stereotype.Component;
import tools.jackson.core.JacksonException;
import tools.jackson.databind.json.JsonMapper;

/**
 * Receives application events from RabbitMQ. Spring AMQP calls this for
 * each message on the queue and acknowledges it only if the method returns
 * normally; if it throws, the message is retried (see application.yml) and
 * then moved to the dead-letter queue.
 */
@Component
public class ApplicationEventListener {

    private final ApplicationEventHandler handler;
    private final JsonMapper json;

    public ApplicationEventListener(ApplicationEventHandler handler, JsonMapper json) {
        this.handler = handler;
        this.json = json;
    }

    @RabbitListener(queues = MessagingConfig.APPLICATION_EVENTS_QUEUE)
    public void onMessage(Message message) {
        var routingKey = message.getMessageProperties().getReceivedRoutingKey();
        var messageId = message.getMessageProperties().getMessageId();
        try {
            switch (routingKey) {
                case "application.changed" ->
                        handler.handleChanged(messageId, json.readValue(message.getBody(), ApplicationChangedEvent.class));
                case "application.deleted" ->
                        handler.handleDeleted(messageId, json.readValue(message.getBody(), ApplicationDeletedEvent.class));
                default -> {
                    // Not ours to handle (a newer event type): acknowledge and move on.
                }
            }
        } catch (JacksonException e) {
            // Malformed JSON will never succeed: straight to the dead-letter queue, no retries.
            throw new AmqpRejectAndDontRequeueException("Unreadable " + routingKey + " message " + messageId, e);
        }
    }
}
