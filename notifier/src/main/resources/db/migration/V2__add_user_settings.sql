-- Each user's reminder choices, from "user.reminder-settings-changed".
-- Users without a row get the defaults from application.yml.
CREATE TABLE user_settings (
    user_id              UUID PRIMARY KEY,
    enabled              BOOLEAN NOT NULL,
    follow_up_days       INT     NOT NULL,
    move_on_days         INT     NOT NULL,
    after_interview_days INT     NOT NULL,
    offer_reply_days     INT     NOT NULL,
    last_event_at        TIMESTAMP WITH TIME ZONE NOT NULL
);
