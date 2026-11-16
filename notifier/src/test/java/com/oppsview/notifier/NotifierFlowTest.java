package com.oppsview.notifier;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.doThrow;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.reset;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;

import com.oppsview.notifier.events.ApplicationEventListener;
import com.oppsview.notifier.events.ReminderDueEvent;
import com.oppsview.notifier.reminders.DueReminderJob;
import com.oppsview.notifier.reminders.ReminderPublisher;
import com.oppsview.notifier.store.NotifierStore;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.time.Instant;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.amqp.AmqpRejectAndDontRequeueException;
import org.springframework.amqp.core.Message;
import org.springframework.amqp.core.MessageProperties;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Primary;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.context.bean.override.mockito.MockitoBean;

/**
 * The notifier end to end inside one process: a message as the .NET API
 * sends it goes through the listener, the handler and the database, and the
 * scheduled job then publishes due reminders. RabbitMQ is replaced by
 * building messages directly and a mocked publisher; the database is H2.
 */
@SpringBootTest
@ActiveProfiles("test")
class NotifierFlowTest {

    static final Instant START = Instant.parse("2026-11-10T12:00:00Z");

    @TestConfiguration
    static class TestClock {
        @Bean
        @Primary
        MutableClock testClock() {
            return new MutableClock(START);
        }
    }

    @Autowired ApplicationEventListener listener;
    @Autowired DueReminderJob job;
    @Autowired NotifierStore store;
    @Autowired MutableClock clock;
    @MockitoBean ReminderPublisher publisher;
    @Autowired org.springframework.jdbc.core.simple.JdbcClient jdbc;

    final UUID app = UUID.randomUUID();
    final UUID user = UUID.randomUUID();

    @BeforeEach
    void fresh_start() {
        // The tests share one in-memory database: start each with empty tables.
        jdbc.sql("DELETE FROM reminder").update();
        jdbc.sql("DELETE FROM tracked_application").update();
        jdbc.sql("DELETE FROM processed_message").update();
        clock.set(START);
        reset(publisher);
    }

    /** JSON exactly as the .NET API's OutboxInterceptor writes it. */
    private Message changed(String status, String previous, String statusChangedAt, String occurredAt) {
        var json = """
                {"applicationId":"%s","userId":"%s","company":"Discovery","role":"Graduate Developer",
                 "platform":"CompanyPortal","status":"%s","previousStatus":%s,"appliedOn":"2026-11-10",
                 "statusChangedAt":"%s","occurredAt":"%s","version":1}
                """.formatted(app, user, status, previous == null ? "null" : "\"" + previous + "\"",
                statusChangedAt, occurredAt);
        return message("application.changed", json);
    }

    private static Message message(String routingKey, String json) {
        var props = new MessageProperties();
        props.setReceivedRoutingKey(routingKey);
        props.setMessageId(UUID.randomUUID().toString());
        return new Message(json.getBytes(StandardCharsets.UTF_8), props);
    }

    @Test
    void a_new_application_gets_follow_up_reminders() {
        listener.onMessage(changed("Applied", null, "2026-11-10T12:00:00Z", "2026-11-10T12:00:00Z"));

        assertThat(store.remindersFor(app)).containsExactly("FOLLOW_UP", "MOVE_ON");
    }

    @Test
    void an_interview_cancels_the_waiting_reminders_and_plans_the_next_one() {
        listener.onMessage(changed("Applied", null, "2026-11-10T12:00:00Z", "2026-11-10T12:00:00Z"));
        listener.onMessage(changed("Interview", "Applied", "2026-11-12T09:00:00Z", "2026-11-12T09:00:00Z"));

        assertThat(store.remindersFor(app)).containsExactly("AFTER_INTERVIEW");
    }

    @Test
    void a_rejection_cancels_everything() {
        listener.onMessage(changed("Applied", null, "2026-11-10T12:00:00Z", "2026-11-10T12:00:00Z"));
        listener.onMessage(changed("Rejected", "Applied", "2026-11-11T09:00:00Z", "2026-11-11T09:00:00Z"));

        assertThat(store.remindersFor(app)).isEmpty();
    }

    @Test
    void the_same_message_delivered_twice_is_handled_once() {
        var once = changed("Applied", null, "2026-11-10T12:00:00Z", "2026-11-10T12:00:00Z");
        listener.onMessage(changed("Interview", "Applied", "2026-11-12T09:00:00Z", "2026-11-12T09:00:00Z"));
        // An earlier event arriving late, and a duplicate of it, change nothing.
        listener.onMessage(once);
        listener.onMessage(once);

        assertThat(store.findApplication(app)).get().extracting(NotifierStore.TrackedApplication::status).isEqualTo("Interview");
        assertThat(store.remindersFor(app)).containsExactly("AFTER_INTERVIEW");
    }

    @Test
    void deleting_the_application_removes_its_reminders() {
        listener.onMessage(changed("Applied", null, "2026-11-10T12:00:00Z", "2026-11-10T12:00:00Z"));
        listener.onMessage(message("application.deleted",
                "{\"applicationId\":\"%s\",\"userId\":\"%s\",\"occurredAt\":\"2026-11-11T00:00:00Z\"}".formatted(app, user)));

        assertThat(store.remindersFor(app)).isEmpty();
        assertThat(store.findApplication(app)).isEmpty();
    }

    @Test
    void due_reminders_are_published_once_with_their_message() {
        listener.onMessage(changed("Applied", null, "2026-11-10T12:00:00Z", "2026-11-10T12:00:00Z"));

        clock.set(Instant.parse("2026-11-17T07:59:00Z"));
        job.sendDueReminders();
        verify(publisher, never()).publish(any());

        clock.set(Instant.parse("2026-11-17T08:00:00Z"));
        job.sendDueReminders();
        var sent = ArgumentCaptor.forClass(ReminderDueEvent.class);
        verify(publisher, times(1)).publish(sent.capture());
        assertThat(sent.getValue().applicationId()).isEqualTo(app);
        assertThat(sent.getValue().userId()).isEqualTo(user);
        assertThat(sent.getValue().kind()).isEqualTo("FOLLOW_UP");
        assertThat(sent.getValue().message()).contains("Discovery");

        clock.advance(Duration.ofMinutes(1));
        job.sendDueReminders();
        verify(publisher, times(1)).publish(any()); // not again
    }

    @Test
    void a_sent_reminder_is_not_sent_again_when_details_change() {
        listener.onMessage(changed("Applied", null, "2026-11-10T12:00:00Z", "2026-11-10T12:00:00Z"));
        clock.set(Instant.parse("2026-11-17T08:00:00Z"));
        job.sendDueReminders();

        // The user renames the job title: same application, same applied date.
        listener.onMessage(changed("Applied", null, "2026-11-10T12:00:00Z", "2026-11-17T09:00:00Z"));
        clock.advance(Duration.ofHours(2));
        job.sendDueReminders();

        verify(publisher, times(1)).publish(any());
    }

    @Test
    void if_publishing_fails_the_reminder_is_retried_next_time() {
        listener.onMessage(changed("Applied", null, "2026-11-10T12:00:00Z", "2026-11-10T12:00:00Z"));
        clock.set(Instant.parse("2026-11-17T08:00:00Z"));
        doThrow(new IllegalStateException("RabbitMQ down")).when(publisher).publish(any());

        assertThat(job.sendDueReminders()).isZero();

        reset(publisher);
        assertThat(job.sendDueReminders()).isEqualTo(1);
    }

    @Test
    void unreadable_messages_are_rejected_without_retrying() {
        org.assertj.core.api.Assertions.assertThatThrownBy(() -> listener.onMessage(message("application.changed", "{not json")))
                .isInstanceOf(AmqpRejectAndDontRequeueException.class);
    }

    @Test
    void unknown_event_types_are_ignored() {
        listener.onMessage(message("application.archived", "{}"));
        assertThat(store.findApplication(app)).isEmpty();
    }
}
