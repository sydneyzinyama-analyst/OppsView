package com.oppsview.notifier.events;

import java.time.Instant;
import java.util.UUID;

/** "reminder.due": sent by the notifier for the .NET API to show the user. */
public record ReminderDueEvent(
        UUID reminderId,
        UUID applicationId,
        UUID userId,
        String kind,
        String company,
        String role,
        String message,
        Instant dueAt,
        int version) {
}
