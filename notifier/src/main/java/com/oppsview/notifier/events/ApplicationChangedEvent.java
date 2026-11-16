package com.oppsview.notifier.events;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import java.time.Instant;
import java.time.LocalDate;
import java.util.UUID;

/**
 * "application.changed" from the .NET API: the application's whole current
 * state. Field names match the API's JSON (camelCase).
 */
@JsonIgnoreProperties(ignoreUnknown = true) // newer API versions may add fields
public record ApplicationChangedEvent(
        UUID applicationId,
        UUID userId,
        String company,
        String role,
        String platform,
        String status,
        String previousStatus,
        LocalDate appliedOn,
        Instant statusChangedAt,
        Instant occurredAt) {
}
