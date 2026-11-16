package com.oppsview.notifier;

import static org.assertj.core.api.Assertions.assertThat;

import com.oppsview.notifier.reminders.PlannedReminder;
import com.oppsview.notifier.reminders.ReminderKind;
import com.oppsview.notifier.reminders.ReminderRules;
import com.oppsview.notifier.reminders.ReminderSettings;
import java.time.Instant;
import java.time.LocalDate;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class ReminderRulesTest {

    private static final ReminderSettings S = ReminderSettings.DEFAULTS;
    private static final LocalDate APPLIED = LocalDate.parse("2026-11-10");
    private static final Instant NOW = Instant.parse("2026-11-10T12:00:00Z");

    private static List<PlannedReminder> plan(String status, Instant statusSince) {
        return ReminderRules.plan("Discovery", "Graduate Developer", status, APPLIED, statusSince, S, NOW);
    }

    @ParameterizedTest
    @ValueSource(strings = {"Applied", "Viewed"})
    void waiting_for_a_reply_plans_a_follow_up_and_a_move_on(String status) {
        var reminders = plan(status, NOW);

        assertThat(reminders).extracting(PlannedReminder::kind).containsExactly(ReminderKind.FOLLOW_UP, ReminderKind.MOVE_ON);
        // 7 and 21 days after applying, at 08:00 UTC (10:00 in South Africa).
        assertThat(reminders.get(0).dueAt()).isEqualTo(Instant.parse("2026-11-17T08:00:00Z"));
        assertThat(reminders.get(1).dueAt()).isEqualTo(Instant.parse("2026-12-01T08:00:00Z"));
        assertThat(reminders.get(0).message()).contains("Discovery", "Graduate Developer", "7 days");
    }

    @Test
    void being_viewed_does_not_restart_the_clock() {
        var viewedLater = Instant.parse("2026-11-14T09:00:00Z");
        assertThat(plan("Viewed", viewedLater).getFirst().dueAt()).isEqualTo(Instant.parse("2026-11-17T08:00:00Z"));
    }

    @Test
    void an_interview_plans_a_follow_up_after_the_invitation() {
        var invited = Instant.parse("2026-11-12T15:30:00Z");

        var reminders = plan("Interview", invited);

        assertThat(reminders).singleElement().satisfies(r -> {
            assertThat(r.kind()).isEqualTo(ReminderKind.AFTER_INTERVIEW);
            assertThat(r.anchor()).isEqualTo(LocalDate.parse("2026-11-12"));
            assertThat(r.dueAt()).isEqualTo(Instant.parse("2026-11-17T08:00:00Z"));
        });
    }

    @Test
    void an_offer_plans_a_reply_reminder() {
        assertThat(plan("Offer", NOW)).extracting(PlannedReminder::kind).containsExactly(ReminderKind.REPLY_TO_OFFER);
    }

    @Test
    void a_rejection_plans_nothing() {
        assertThat(plan("Rejected", NOW)).isEmpty();
    }

    @Test
    void reminders_long_overdue_when_planned_are_skipped() {
        // Applied 40 days ago (e.g. imported from old emails): follow-up and move-on
        // would both be weeks overdue, so neither is sent.
        var oldApplied = LocalDate.parse("2026-10-01");
        var reminders = ReminderRules.plan("Acme", "Dev", "Applied", oldApplied, NOW, S, NOW);
        assertThat(reminders).isEmpty();
    }

    @Test
    void a_reminder_only_slightly_overdue_is_still_sent() {
        // Applied 10 days ago: the follow-up was due 3 days ago, within the 7-day grace.
        var reminders = ReminderRules.plan("Acme", "Dev", "Applied", LocalDate.parse("2026-10-31"), NOW, S, NOW);
        assertThat(reminders).extracting(PlannedReminder::kind).contains(ReminderKind.FOLLOW_UP);
    }

    @Test
    void zero_days_switches_a_reminder_off() {
        var noMoveOn = new ReminderSettings(7, 0, 5, 2, 8, 7);
        var reminders = ReminderRules.plan("Acme", "Dev", "Applied", APPLIED, NOW, noMoveOn, NOW);
        assertThat(reminders).extracting(PlannedReminder::kind).containsExactly(ReminderKind.FOLLOW_UP);
    }
}
