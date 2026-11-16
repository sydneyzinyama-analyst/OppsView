package com.oppsview.notifier.reminders;

import com.oppsview.notifier.events.ReminderDueEvent;
import com.oppsview.notifier.store.NotifierStore;
import java.time.Clock;
import java.time.Duration;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

/**
 * Every minute: send the reminders that are due, then mark them sent. If
 * RabbitMQ is unavailable, the reminder stays unsent and is tried again next
 * minute.
 */
@Component
public class DueReminderJob {

    private static final Logger log = LoggerFactory.getLogger(DueReminderJob.class);
    private static final int BATCH = 100;

    private final NotifierStore store;
    private final ReminderPublisher publisher;
    private final Clock clock;

    public DueReminderJob(NotifierStore store, ReminderPublisher publisher, Clock clock) {
        this.store = store;
        this.publisher = publisher;
        this.clock = clock;
    }

    @Scheduled(fixedDelayString = "${notifier.check-interval:PT1M}", initialDelayString = "${notifier.initial-delay:PT10S}")
    public int sendDueReminders() {
        var now = clock.instant();
        var sent = 0;
        for (var r : store.findDue(now, BATCH)) {
            try {
                publisher.publish(new ReminderDueEvent(r.id(), r.applicationId(), r.userId(), r.kind(), r.company(),
                        r.role(), r.message(), r.dueAt(), 1));
                store.markSent(r.id(), now);
                sent++;
            } catch (RuntimeException e) {
                log.warn("Could not send reminder {}; will retry: {}", r.id(), e.getMessage());
                break;
            }
        }
        if (sent > 0) {
            log.info("Sent {} reminder(s)", sent);
        }
        // Message ids only need remembering for as long as a redelivery could happen.
        store.pruneProcessedBefore(now.minus(Duration.ofDays(14)));
        return sent;
    }
}
