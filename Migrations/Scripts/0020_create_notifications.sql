-- Outbox of notifications to be delivered to a recipient email. A sender (deferred,
-- ties into the async job runner) drains rows where sent_at IS NULL.
CREATE TABLE notifications (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    recipient_email text NOT NULL,
    subject         text NOT NULL,
    body            text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    sent_at         timestamptz NULL
);

CREATE INDEX ix_notifications_unsent ON notifications (created_at) WHERE sent_at IS NULL;
