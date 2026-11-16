package com.oppsview.notifier.reminders;

import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.context.properties.bind.DefaultValue;

/**
 * How many days until each reminder, from application.yml
 * ("notifier.reminders.*"). Users can override them in OppsView.
 *
 * @param followUpDays      days after applying, with no reply, to suggest a follow-up
 * @param moveOnDays        days after applying, with no reply, to suggest moving on
 * @param afterInterviewDays days after an interview invitation to suggest a thank-you / feedback request
 * @param offerReplyDays    days after an offer to check it was answered
 * @param hourUtc           hour of day (UTC) reminders become due; 8 = 10:00 in South Africa
 * @param maxOverdueDays    reminders already this overdue when planned are skipped
 *                          (stops a flood when old emails are imported)
 */
@ConfigurationProperties("notifier.reminders")
public record ReminderSettings(
        // Defaults, so a missing setting never silently switches reminders off.
        @DefaultValue("7") int followUpDays,
        @DefaultValue("21") int moveOnDays,
        @DefaultValue("5") int afterInterviewDays,
        @DefaultValue("2") int offerReplyDays,
        @DefaultValue("8") int hourUtc,
        @DefaultValue("7") int maxOverdueDays) {

    public static final ReminderSettings DEFAULTS = new ReminderSettings(7, 21, 5, 2, 8, 7);

    /** These settings with a user's own day counts (time of day and overdue rule stay global). */
    public ReminderSettings forUser(int followUp, int moveOn, int afterInterview, int offerReply) {
        return new ReminderSettings(followUp, moveOn, afterInterview, offerReply, hourUtc, maxOverdueDays);
    }

    /** Everything off: used when a user switches reminders off. */
    public ReminderSettings allOff() {
        return forUser(0, 0, 0, 0);
    }
}
