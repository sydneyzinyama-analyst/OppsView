package com.oppsview.notifier.config;

import java.time.Clock;
import org.springframework.amqp.core.Binding;
import org.springframework.amqp.core.BindingBuilder;
import org.springframework.amqp.core.FanoutExchange;
import org.springframework.amqp.core.Queue;
import org.springframework.amqp.core.QueueBuilder;
import org.springframework.amqp.core.TopicExchange;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

/**
 * The RabbitMQ setup this service needs. Spring declares these on startup
 * (and they're idempotent: declaring what already exists changes nothing).
 *
 * <pre>
 * exchange "oppsview.events" (topic)
 *   ├─ application.*, user.reminder-settings-changed
 *   │                  ─▶ queue "notifier.application-events" ─▶ this service
 *   └─ reminder.due   ─▶ (the .NET API's own queue)
 * messages that keep failing ─▶ "oppsview.dead-letter" ─▶ queue "notifier.application-events.dlq"
 * </pre>
 */
@Configuration
public class MessagingConfig {

    public static final String EXCHANGE = "oppsview.events";
    public static final String APPLICATION_EVENTS_QUEUE = "notifier.application-events";
    public static final String DEAD_LETTER_EXCHANGE = "oppsview.dead-letter";
    public static final String DEAD_LETTER_QUEUE = "notifier.application-events.dlq";
    public static final String REMINDER_DUE = "reminder.due";

    @Bean
    TopicExchange eventsExchange() {
        return new TopicExchange(EXCHANGE, true, false);
    }

    @Bean
    Queue applicationEventsQueue() {
        // Durable: messages wait here while the notifier is switched off.
        return QueueBuilder.durable(APPLICATION_EVENTS_QUEUE)
                .deadLetterExchange(DEAD_LETTER_EXCHANGE)
                .build();
    }

    @Bean
    Binding applicationEventsBinding() {
        return BindingBuilder.bind(applicationEventsQueue()).to(eventsExchange()).with("application.*");
    }

    @Bean
    Binding settingsBinding() {
        return BindingBuilder.bind(applicationEventsQueue()).to(eventsExchange()).with("user.reminder-settings-changed");
    }

    @Bean
    FanoutExchange deadLetterExchange() {
        return new FanoutExchange(DEAD_LETTER_EXCHANGE, true, false);
    }

    @Bean
    Queue deadLetterQueue() {
        return QueueBuilder.durable(DEAD_LETTER_QUEUE).build();
    }

    @Bean
    Binding deadLetterBinding() {
        return BindingBuilder.bind(deadLetterQueue()).to(deadLetterExchange());
    }

    /** The clock, injected so tests can control time. */
    @Bean
    Clock clock() {
        return Clock.systemUTC();
    }
}
