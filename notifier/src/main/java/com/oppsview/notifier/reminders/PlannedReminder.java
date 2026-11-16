package com.oppsview.notifier.reminders;

import java.time.Instant;
import java.time.LocalDate;

/**
 * A reminder that should exist for an application's current state.
 *
 * @param anchor the date the reminder counts from (applied date, or the day the
 *               status started). Together with the kind it identifies the
 *               reminder, so re-planning after an unrelated change (e.g. a new
 *               job title) neither duplicates nor resends it.
 */
public record PlannedReminder(ReminderKind kind, LocalDate anchor, Instant dueAt, String message) {
}
