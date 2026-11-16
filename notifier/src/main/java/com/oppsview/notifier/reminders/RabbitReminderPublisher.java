package com.oppsview.notifier.reminders;

import com.oppsview.notifier.config.MessagingConfig;
import com.oppsview.notifier.events.ReminderDueEvent;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.TimeUnit;
import org.springframework.amqp.core.MessageBuilder;
import org.springframework.amqp.core.MessageDeliveryMode;
import org.springframework.amqp.rabbit.connection.CorrelationData;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.stereotype.Component;
import tools.jackson.databind.json.JsonMapper;

@Component
public class RabbitReminderPublisher implements ReminderPublisher {

    private final RabbitTemplate rabbit;
    private final JsonMapper json;

    public RabbitReminderPublisher(RabbitTemplate rabbit, JsonMapper json) {
        this.rabbit = rabbit;
        this.json = json;
    }

    @Override
    public void publish(ReminderDueEvent event) {
        var message = MessageBuilder.withBody(json.writeValueAsString(event).getBytes(StandardCharsets.UTF_8))
                .setContentType("application/json")
                .setMessageId(event.reminderId().toString()) // the API ignores a duplicate
                .setType(MessagingConfig.REMINDER_DUE)
                .setDeliveryMode(MessageDeliveryMode.PERSISTENT)
                .build();

        // Publisher confirms: wait for RabbitMQ to say it has the message
        // before the reminder is marked as sent.
        var correlation = new CorrelationData(event.reminderId().toString());
        rabbit.send(MessagingConfig.EXCHANGE, MessagingConfig.REMINDER_DUE, message, correlation);
        try {
            var confirm = correlation.getFuture().get(10, TimeUnit.SECONDS);
            if (!confirm.ack()) {
                throw new IllegalStateException("RabbitMQ refused reminder " + event.reminderId() + ": " + confirm.reason());
            }
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException("Interrupted while publishing", e);
        } catch (java.util.concurrent.ExecutionException | java.util.concurrent.TimeoutException e) {
            throw new IllegalStateException("No confirmation from RabbitMQ for reminder " + event.reminderId(), e);
        }
    }
}
