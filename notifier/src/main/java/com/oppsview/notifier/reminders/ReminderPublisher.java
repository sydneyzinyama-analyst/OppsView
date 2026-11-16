package com.oppsview.notifier.reminders;

import com.oppsview.notifier.events.ReminderDueEvent;

/** Sends a "reminder.due" event. An interface so the job can be tested without RabbitMQ. */
public interface ReminderPublisher {

    /** Returns once RabbitMQ has confirmed it has the message; throws otherwise. */
    void publish(ReminderDueEvent event);
}
