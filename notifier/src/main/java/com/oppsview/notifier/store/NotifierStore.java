package com.oppsview.notifier.store;

import com.oppsview.notifier.reminders.PlannedReminder;
import java.time.Instant;
import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Repository;

/**
 * All of the notifier's SQL, in one place. JdbcClient (Spring 6.1+) runs
 * plain SQL with named parameters: no ORM, nothing hidden.
 */
@Repository
public class NotifierStore {

    public record TrackedApplication(UUID applicationId, UUID userId, String company, String role, String status,
            LocalDate appliedOn, Instant statusChangedAt, Instant lastEventAt) {
    }

    public record DueReminder(UUID id, UUID applicationId, UUID userId, String kind, String company, String role,
            String message, Instant dueAt) {
    }

    private final JdbcClient jdbc;

    public NotifierStore(JdbcClient jdbc) {
        this.jdbc = jdbc;
    }

    // ---- Idempotency -----------------------------------------------------

    /** Records the message id; false if it was already processed. */
    public boolean markProcessed(String messageId, Instant now) {
        var exists = jdbc.sql("SELECT COUNT(*) FROM processed_message WHERE message_id = :id")
                .param("id", messageId).query(Integer.class).single() > 0;
        if (exists) {
            return false;
        }
        jdbc.sql("INSERT INTO processed_message (message_id, processed_at) VALUES (:id, :at)")
                .param("id", messageId).param("at", utc(now)).update();
        return true;
    }

    public int pruneProcessedBefore(Instant before) {
        return jdbc.sql("DELETE FROM processed_message WHERE processed_at < :before")
                .param("before", utc(before)).update();
    }

    // ---- Applications ----------------------------------------------------

    public Optional<TrackedApplication> findApplication(UUID applicationId) {
        return jdbc.sql("""
                SELECT application_id, user_id, company, role, status, applied_on, status_changed_at, last_event_at
                FROM tracked_application WHERE application_id = :id
                """)
                .param("id", applicationId)
                .query((rs, n) -> new TrackedApplication(
                        rs.getObject("application_id", UUID.class),
                        rs.getObject("user_id", UUID.class),
                        rs.getString("company"),
                        rs.getString("role"),
                        rs.getString("status"),
                        rs.getObject("applied_on", LocalDate.class),
                        rs.getObject("status_changed_at", OffsetDateTime.class).toInstant(),
                        rs.getObject("last_event_at", OffsetDateTime.class).toInstant()))
                .optional();
    }

    public void saveApplication(TrackedApplication a) {
        var params = new java.util.HashMap<String, Object>();
        params.put("id", a.applicationId());
        params.put("user", a.userId());
        params.put("company", a.company());
        params.put("role", a.role());
        params.put("status", a.status());
        params.put("appliedOn", a.appliedOn());
        params.put("statusAt", utc(a.statusChangedAt()));
        params.put("eventAt", utc(a.lastEventAt()));

        var updated = jdbc.sql("""
                UPDATE tracked_application
                SET user_id = :user, company = :company, role = :role, status = :status, applied_on = :appliedOn,
                    status_changed_at = :statusAt, last_event_at = :eventAt
                WHERE application_id = :id
                """).params(params).update();
        if (updated == 0) {
            jdbc.sql("""
                    INSERT INTO tracked_application
                        (application_id, user_id, company, role, status, applied_on, status_changed_at, last_event_at)
                    VALUES (:id, :user, :company, :role, :status, :appliedOn, :statusAt, :eventAt)
                    """).params(params).update();
        }
    }

    public void deleteApplication(UUID applicationId) {
        jdbc.sql("DELETE FROM reminder WHERE application_id = :id").param("id", applicationId).update();
        jdbc.sql("DELETE FROM tracked_application WHERE application_id = :id").param("id", applicationId).update();
    }

    // ---- Reminders -------------------------------------------------------

    /**
     * Makes the application's reminders match the plan: unsent reminders not
     * in the plan are cancelled, missing ones are added. Ones already sent
     * are left alone, so a reminder is never sent twice for the same thing.
     */
    public void replaceUnsentReminders(UUID applicationId, UUID userId, List<PlannedReminder> plan) {
        var existing = jdbc.sql("SELECT kind, anchor, sent_at FROM reminder WHERE application_id = :id")
                .param("id", applicationId)
                .query((rs, n) -> new Object[] {rs.getString("kind"), rs.getObject("anchor", LocalDate.class),
                        rs.getObject("sent_at", OffsetDateTime.class)})
                .list();

        for (var row : existing) {
            var kind = (String) row[0];
            var anchor = (LocalDate) row[1];
            var stillPlanned = plan.stream().anyMatch(p -> p.kind().name().equals(kind) && p.anchor().equals(anchor));
            if (row[2] == null && !stillPlanned) {
                jdbc.sql("DELETE FROM reminder WHERE application_id = :id AND kind = :kind AND anchor = :anchor")
                        .param("id", applicationId).param("kind", kind).param("anchor", anchor).update();
            }
        }

        for (var p : plan) {
            var alreadyThere = existing.stream()
                    .anyMatch(row -> row[0].equals(p.kind().name()) && row[1].equals(p.anchor()));
            if (alreadyThere) {
                // Same reminder: refresh the wording (e.g. the company was renamed) unless it's gone out.
                jdbc.sql("""
                        UPDATE reminder SET message = :message, due_at = :due
                        WHERE application_id = :id AND kind = :kind AND anchor = :anchor AND sent_at IS NULL
                        """)
                        .param("message", p.message()).param("due", utc(p.dueAt()))
                        .param("id", applicationId).param("kind", p.kind().name()).param("anchor", p.anchor())
                        .update();
            } else {
                jdbc.sql("""
                        INSERT INTO reminder (id, application_id, user_id, kind, anchor, due_at, message)
                        VALUES (:rid, :id, :user, :kind, :anchor, :due, :message)
                        """)
                        .param("rid", UUID.randomUUID()).param("id", applicationId).param("user", userId)
                        .param("kind", p.kind().name()).param("anchor", p.anchor()).param("due", utc(p.dueAt()))
                        .param("message", p.message())
                        .update();
            }
        }
    }

    public List<DueReminder> findDue(Instant now, int limit) {
        return jdbc.sql("""
                SELECT r.id, r.application_id, r.user_id, r.kind, a.company, a.role, r.message, r.due_at
                FROM reminder r JOIN tracked_application a ON a.application_id = r.application_id
                WHERE r.sent_at IS NULL AND r.due_at <= :now
                ORDER BY r.due_at
                FETCH FIRST :limit ROWS ONLY
                """)
                .param("now", utc(now)).param("limit", limit)
                .query((rs, n) -> new DueReminder(
                        rs.getObject("id", UUID.class),
                        rs.getObject("application_id", UUID.class),
                        rs.getObject("user_id", UUID.class),
                        rs.getString("kind"),
                        rs.getString("company"),
                        rs.getString("role"),
                        rs.getString("message"),
                        rs.getObject("due_at", OffsetDateTime.class).toInstant()))
                .list();
    }

    public void markSent(UUID reminderId, Instant now) {
        jdbc.sql("UPDATE reminder SET sent_at = :now WHERE id = :id")
                .param("now", utc(now)).param("id", reminderId).update();
    }

    public List<String> remindersFor(UUID applicationId) {
        return jdbc.sql("SELECT kind FROM reminder WHERE application_id = :id ORDER BY due_at")
                .param("id", applicationId).query(String.class).list();
    }

    private static OffsetDateTime utc(Instant instant) {
        return instant.atOffset(ZoneOffset.UTC);
    }
}
