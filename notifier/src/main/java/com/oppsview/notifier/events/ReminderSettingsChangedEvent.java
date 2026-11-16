package com.oppsview.notifier.events;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import java.time.Instant;
import java.util.UUID;

/** "user.reminder-settings-changed" from the .NET API. */
@JsonIgnoreProperties(ignoreUnknown = true)
public record ReminderSettingsChangedEvent(
        UUID userId,
        boolean enabled,
        int followUpDays,
        int moveOnDays,
        int afterInterviewDays,
        int offerReplyDays,
        Instant occurredAt) {
}
