package com.oppsview.notifier.events;

import com.oppsview.notifier.reminders.ReminderRules;
import com.oppsview.notifier.reminders.ReminderSettings;
import com.oppsview.notifier.store.NotifierStore;
import com.oppsview.notifier.store.NotifierStore.TrackedApplication;
import java.time.Clock;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * What to do with each event: keep the copy of the application up to date
 * and re-plan its reminders. One database transaction per message, so a
 * failure leaves nothing half done (and RabbitMQ redelivers the message).
 */
@Service
public class ApplicationEventHandler {

    private static final Logger log = LoggerFactory.getLogger(ApplicationEventHandler.class);

    private final NotifierStore store;
    private final ReminderSettings settings;
    private final Clock clock;

    public ApplicationEventHandler(NotifierStore store, ReminderSettings settings, Clock clock) {
        this.store = store;
        this.settings = settings;
        this.clock = clock;
    }

    @Transactional
    public void handleChanged(String messageId, ApplicationChangedEvent e) {
        var now = clock.instant();
        if (!store.markProcessed(messageId, now)) {
            log.debug("Ignoring duplicate message {}", messageId);
            return;
        }

        var existing = store.findApplication(e.applicationId());
        if (existing.isPresent() && existing.get().lastEventAt().isAfter(e.occurredAt())) {
            // An older event arriving after a newer one: the newer state wins.
            log.debug("Ignoring out-of-date event for {}", e.applicationId());
            return;
        }

        store.saveApplication(new TrackedApplication(e.applicationId(), e.userId(), e.company(), e.role(), e.status(),
                e.appliedOn(), e.statusChangedAt(), e.occurredAt()));
        var plan = ReminderRules.plan(e.company(), e.role(), e.status(), e.appliedOn(), e.statusChangedAt(),
                settings, now);
        store.replaceUnsentReminders(e.applicationId(), e.userId(), plan);
    }

    @Transactional
    public void handleDeleted(String messageId, ApplicationDeletedEvent e) {
        if (store.markProcessed(messageId, clock.instant())) {
            store.deleteApplication(e.applicationId());
        }
    }
}
