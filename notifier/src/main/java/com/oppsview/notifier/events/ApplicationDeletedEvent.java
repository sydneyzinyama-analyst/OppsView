package com.oppsview.notifier.events;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import java.time.Instant;
import java.util.UUID;

/** "application.deleted" from the .NET API. */
@JsonIgnoreProperties(ignoreUnknown = true)
public record ApplicationDeletedEvent(UUID applicationId, UUID userId, Instant occurredAt) {
}
