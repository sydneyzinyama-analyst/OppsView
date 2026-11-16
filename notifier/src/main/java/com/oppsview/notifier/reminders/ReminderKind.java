package com.oppsview.notifier.reminders;

/** The kinds of reminder the notifier sends. */
public enum ReminderKind {
    /** Applied a while ago with no reply: send a polite follow-up. */
    FOLLOW_UP,
    /** Applied long ago with no reply: probably time to move on. */
    MOVE_ON,
    /** Some days after an interview invitation: thank-you note or ask for feedback. */
    AFTER_INTERVIEW,
    /** An offer is waiting for an answer. */
    REPLY_TO_OFFER
}
