package com.oppsview.notifier.reminders;

import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.List;

/**
 * The reminder rules, as pure functions: given an application's state, which
 * reminders should exist? No database, no RabbitMQ, so every rule is easy to
 * unit test.
 */
public final class ReminderRules {

    private ReminderRules() {
    }

    /**
     * @param status      the application's status ("Applied", "Viewed", "Interview", "Offer", "Rejected")
     * @param appliedOn   when the user applied
     * @param statusSince when the current status started
     * @param now         the current time, to skip reminders long overdue
     */
    public static List<PlannedReminder> plan(String company, String role, String status, LocalDate appliedOn,
            Instant statusSince, ReminderSettings s, Instant now) {
        var since = statusSince.atZone(ZoneOffset.UTC).toLocalDate();
        var planned = new ArrayList<PlannedReminder>();

        switch (status) {
            case "Applied", "Viewed" -> {
                // Counted from the applied date: "viewed" isn't a reply.
                add(planned, ReminderKind.FOLLOW_UP, appliedOn, s.followUpDays(), s, now,
                        "You applied to %s (%s) %d days ago and haven't heard back. A short, polite follow-up email can help."
                                .formatted(company, role, s.followUpDays()));
                add(planned, ReminderKind.MOVE_ON, appliedOn, s.moveOnDays(), s, now,
                        "It's been %d days since you applied to %s (%s) with no reply. Keep it in mind, but focus your energy on other opportunities."
                                .formatted(s.moveOnDays(), company, role));
            }
            case "Interview" -> add(planned, ReminderKind.AFTER_INTERVIEW, since, s.afterInterviewDays(), s, now,
                    "%s invited you to interview for %s %d days ago. If you've had the interview, send a thank-you or ask for feedback."
                            .formatted(company, role, s.afterInterviewDays()));
            case "Offer" -> add(planned, ReminderKind.REPLY_TO_OFFER, since, s.offerReplyDays(), s, now,
                    "You have an offer from %s for %s. Have you replied yet?".formatted(company, role));
            default -> {
                // Rejected (or unknown): nothing to remind about.
            }
        }
        return planned;
    }

    private static void add(List<PlannedReminder> planned, ReminderKind kind, LocalDate anchor, int days,
            ReminderSettings s, Instant now, String message) {
        if (days <= 0) {
            return; // switched off
        }
        var due = anchor.plusDays(days).atTime(s.hourUtc(), 0).toInstant(ZoneOffset.UTC);
        if (due.isBefore(now.minus(java.time.Duration.ofDays(s.maxOverdueDays())))) {
            return; // long past: sending it now would just be noise
        }
        planned.add(new PlannedReminder(kind, anchor, due, message));
    }
}
