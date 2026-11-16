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
 * What to do with each event: keep the copy of the application (and the
 * user's settings) up to date and re-plan reminders. One database transaction per message, so a
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

        var application = new TrackedApplication(e.applicationId(), e.userId(), e.company(), e.role(), e.status(),
                e.appliedOn(), e.statusChangedAt(), e.occurredAt());
        store.saveApplication(application);
        replan(application, settingsFor(e.userId()), now);
    }

    /**
     * A user changed their reminder settings: store them and re-plan every
     * one of their applications with the new numbers.
     */
    @Transactional
    public void handleSettingsChanged(String messageId, ReminderSettingsChangedEvent e) {
        var now = clock.instant();
        if (!store.markProcessed(messageId, now)) {
            return;
        }
        var existing = store.findSettings(e.userId());
        if (existing.isPresent() && existing.get().lastEventAt().isAfter(e.occurredAt())) {
            return; // an older change arriving late
        }
        store.saveSettings(new NotifierStore.UserSettings(e.userId(), e.enabled(), e.followUpDays(), e.moveOnDays(),
                e.afterInterviewDays(), e.offerReplyDays(), e.occurredAt()));

        var userSettings = settingsFor(e.userId());
        for (var application : store.applicationsOf(e.userId())) {
            replan(application, userSettings, now);
        }
    }

    /** The defaults, with the user's own choices on top (or everything off). */
    private ReminderSettings settingsFor(java.util.UUID userId) {
        return store.findSettings(userId)
                .map(u -> u.enabled()
                        ? settings.forUser(u.followUpDays(), u.moveOnDays(), u.afterInterviewDays(), u.offerReplyDays())
                        : settings.allOff())
                .orElse(settings);
    }

    private void replan(TrackedApplication a, ReminderSettings s, java.time.Instant now) {
        var plan = ReminderRules.plan(a.company(), a.role(), a.status(), a.appliedOn(), a.statusChangedAt(), s, now);
        store.replaceUnsentReminders(a.applicationId(), a.userId(), plan);
    }

    @Transactional
    public void handleDeleted(String messageId, ApplicationDeletedEvent e) {
        if (store.markProcessed(messageId, clock.instant())) {
            store.deleteApplication(e.applicationId());
        }
    }
}
