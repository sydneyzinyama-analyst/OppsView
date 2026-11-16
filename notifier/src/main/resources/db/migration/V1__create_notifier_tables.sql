-- The notifier's own tables, in its own "notifier" schema: each service owns
-- its data. Written to run on PostgreSQL and on H2 (used by the tests).

-- The notifier's copy of each application, kept up to date from events.
CREATE TABLE tracked_application (
    application_id    UUID PRIMARY KEY,
    user_id           UUID         NOT NULL,
    company           VARCHAR(200) NOT NULL,
    role              VARCHAR(200) NOT NULL,
    status            VARCHAR(32)  NOT NULL,
    applied_on        DATE         NOT NULL,
    status_changed_at TIMESTAMP WITH TIME ZONE NOT NULL,
    -- The latest event applied; older events arriving late are ignored.
    last_event_at     TIMESTAMP WITH TIME ZONE NOT NULL
);

CREATE TABLE reminder (
    id             UUID PRIMARY KEY,
    application_id UUID         NOT NULL,
    user_id        UUID         NOT NULL,
    kind           VARCHAR(32)  NOT NULL,
    anchor         DATE         NOT NULL,
    due_at         TIMESTAMP WITH TIME ZONE NOT NULL,
    message        VARCHAR(1000) NOT NULL,
    sent_at        TIMESTAMP WITH TIME ZONE,
    -- A reminder for the same thing (kind + the date it counts from) exists once.
    CONSTRAINT uq_reminder UNIQUE (application_id, kind, anchor)
);

-- "What's due and not sent yet?" runs every minute.
CREATE INDEX ix_reminder_due ON reminder (sent_at, due_at);

-- Messages already handled. RabbitMQ delivers "at least once", so a message
-- can arrive twice; its id here means the second copy is ignored.
CREATE TABLE processed_message (
    message_id   VARCHAR(64) PRIMARY KEY,
    processed_at TIMESTAMP WITH TIME ZONE NOT NULL
);
